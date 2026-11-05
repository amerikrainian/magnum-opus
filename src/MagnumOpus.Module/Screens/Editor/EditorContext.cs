using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;

namespace MagnumOpus.Module.Screens.Editor
{
    /// <summary>
    /// What the solution editor's sections share (Guildrun's pattern: the screen hands one object to
    /// the sections that need it). Positions only, never game objects across frames, except the part
    /// a carry holds, which is out of the solution while carried and goes back (or is discarded) when
    /// the carry ends, the screen's pop included. Declares no nodes.
    /// </summary>
    internal sealed class EditorContext : ScreenSection
    {
        public const string StopBoard = "editor.board";
        public const string StopProgram = "editor.program";
        public const string StopTray = "editor.tray";
        public const string StopLog = "editor.log";
        public const int MaxColumn = 9999;

        private readonly Func<SolutionEditorScreen> _editor;

        public EditorContext(Func<SolutionEditorScreen> editor)
        {
            _editor = editor;
            Carry = new CarryMode(this);
            Draw = new TrackDrawMode(this);
        }

        /// <summary>The live game editor, or null.</summary>
        public SolutionEditorScreen Ed => _editor();

        // ---- cursors ----
        public HexIndex Cursor;
        public int Row, Col;

        // ---- the tape's selection (a rectangle between the anchor and the cursor) and clipboard ----
        public bool Selecting;
        public int AnchorRow, AnchorCol;
        public readonly List<ClipCell> Clipboard = new List<ClipCell>();

        public struct ClipCell { public int Row, Col; public InstructionType Type; }

        // ---- the board's selection (the game's own partSelection) and its clipboard ----
        /// <summary>The first corner of a rectangle being marked, or null.</summary>
        public HexIndex? Corner;

        /// <summary>Copied parts: templates kept off the board (each paste places fresh copies), at
        /// their offsets from the cursor when copied. After a cut, <see cref="CutParts"/> holds the
        /// parts themselves for the first paste (a move); later pastes copy the templates.</summary>
        public readonly List<KeyValuePair<Part, HexIndex>> PartClipboard = new List<KeyValuePair<Part, HexIndex>>();
        public readonly List<KeyValuePair<Part, HexIndex>> CutParts = new List<KeyValuePair<Part, HexIndex>>();

        /// <summary>The board hex Alt+Backspace left for the tape, or null.</summary>
        public HexIndex? JumpHex;

        // ---- modes ----
        public readonly CarryMode Carry;
        public readonly TrackDrawMode Draw;

        /// <summary>A mode Escape ends (so the game does not get it).</summary>
        public bool InMode => Carry.Active || Draw.Active || Selecting
            || (PartsSelected && Equals(Navigation.FocusedStopKey, StopBoard));

        /// <summary>A board selection exists (or a rectangle is half marked).</summary>
        public bool PartsSelected
        {
            get
            {
                var ed = Ed;
                return Corner.HasValue || (ed != null && ed.partSelection != null && ed.partSelection.GetCount() > 0);
            }
        }

        public void Reset()
        {
            Cursor = new HexIndex(0, 0);
            Row = 0;
            Col = 0;
            Selecting = false;
            Corner = null;
            JumpHex = null;
            // Parts copied in another puzzle may not belong in this one.
            PartClipboard.Clear();
            CutParts.Clear();
        }

        public override void OnPop()
        {
            // The editor closing under a carry must not lose the part.
            Carry.Cancel(speak: false);
            Draw.Cancel(speak: false);
        }

        // ---- shared helpers ----

        public static void Say(string text, bool interrupt = true)
        {
            if (!string.IsNullOrEmpty(text)) Speech.Say(text, interrupt);
        }

        /// <summary>Whether the solution may be edited now; says why not when it may not.</summary>
        public bool CanEdit()
        {
            var ed = Ed;
            if (ed == null) return false;
            if (ed.GetSimPlayState() != SimPlayState.Stopped) { Say(Strings.EditorStopToEdit); return false; }
            return true;
        }

        /// <summary>Whether a mode is holding the board (a carry or a track drawing); says so.</summary>
        public bool Busy()
        {
            if (!Carry.Active && !Draw.Active) return false;
            Say(Strings.EditorFinishFirst);
            return true;
        }

        /// <summary>The arm whose base or gripper is on a hex, or null.</summary>
        public Part ArmAt(HexIndex hex)
        {
            var ed = Ed;
            if (ed == null) return null;
            var arms = Board.Arms(ed);
            foreach (var arm in arms) if (Board.BaseHex(ed, arm) == hex) return arm;
            foreach (var arm in arms) if (Board.Grippers(ed, arm).Any(g => g.Key == hex)) return arm;
            return null;
        }

        /// <summary>The part an edit on a hex acts on: an arm (base, then gripper), else what covers
        /// the hex, a track last (it lies under everything).</summary>
        public Part PartAt(HexIndex hex)
        {
            var arm = ArmAt(hex);
            if (arm != null) return arm;
            var ed = Ed;
            var solution = ed?.GetSolution();
            if (solution == null) return null;
            Part track = null;
            foreach (var part in solution.parts)
            {
                if (part.GetType().isProgrammable || !part.GetHexFootprint(solution).Contains(hex)) continue;
                if (part.GetType().isTrack) { track = track ?? part; continue; }
                return part;
            }
            return track;
        }

        /// <summary>A part's facing for speech: an arm reads its base line, a track nothing (its
        /// shape is its hexes), anything else its name and the direction it points.</summary>
        public string Facing(Part part)
        {
            var ed = Ed;
            if (part.GetType().isProgrammable)
            {
                int n = Board.ArmNumber(ed, part);
                if (n > 0) return Board.DescribeArmBase(ed, part, n);
                // Not on the board (carried): no row yet.
                return Strings.EditorPartFacing(Board.PartName(ed, part), Hex.Word(Hex.FromTurns(part.GetRotation().GetNumberOfTurns())))
                    + ", " + Strings.EditorLength(part.GetArmLength());
            }
            if (part.GetType().isTrack) return Strings.EditorRotated;
            return Strings.EditorPartFacing(Board.PartName(ed, part), Hex.Word(Hex.FromTurns(part.GetRotation().GetNumberOfTurns())));
        }

        public static string Error(string message) => GameText.Clean(message ?? "");

        /// <summary>Copied arms numbered right after the highest-numbered part that landed with them,
        /// in their order (the game's drag-copy).</summary>
        public static void NumberCopies(Solution solution, IList<Part> landed, IEnumerable<Part> copies)
        {
            var arms = copies.Where(p => p.GetType().isProgrammable).OrderBy(p => p.programIndex).ToList();
            if (arms.Count == 0) return;
            int next = landed.Max(p => p.programIndex) + 1;
            foreach (var p in arms) solution.SetProgramIndex(p, next++);
        }

        /// <summary>A part as a list names it: an arm on the board by its number, anything else by
        /// its name.</summary>
        public string Label(Part part)
        {
            var ed = Ed;
            int n = part.GetType().isProgrammable ? Board.ArmNumber(ed, part) : 0;
            return n > 0 ? Strings.EditorArm(n) : Board.PartName(ed, part);
        }

        /// <summary>Up to three parts by name, more by count.</summary>
        public string Names(IList<Part> parts)
        {
            if (parts.Count > 3) return Strings.EditorPartCount(parts.Count);
            return string.Join(", ", parts.Select(Label));
        }

        /// <summary>The selected parts when the part under the cursor is one of them (an action
        /// there takes the whole group, the game's drag), else null.</summary>
        public List<Part> GroupAtCursor()
        {
            var ed = Ed;
            if (ed == null || ed.partSelection == null || ed.partSelection.GetCount() == 0) return null;
            var here = PartAt(Cursor);
            if (here == null || !ed.partSelection.IsSelected(here)) return null;
            var solution = ed.GetSolution();
            return ed.partSelection.Enumerable().Where(p => solution.parts.Contains(p)).ToList();
        }

        /// <summary>Whether the game would copy a part (not a reagent, product, unique part or
        /// conduit, nor in a puzzle that limits part counts).</summary>
        public bool Copyable(Part part)
        {
            var type = part.GetType();
            return !type.isInput && !type.IsOutput() && !type.isUnique && !type.isConduit
                && Ed.GetSolution().GetPuzzle().limitedPartAmounts.Count == 0;
        }
    }

    /// <summary>
    /// Parts picked up on the board (the game's drag, by keyboard): one part, or a group (the game's
    /// selection dragged together). Out of the solution while carried (new parts and copies were
    /// never in it), moved with the hex keys keeping their places relative to the cursor, rotated
    /// round the cursor, put down with Enter where the game's own placement check allows every one
    /// (its reason spoken, with the part's name in a group, where it does not), cancelled with Escape
    /// (the placed ones back where they were, the new ones discarded).
    /// </summary>
    internal sealed class CarryMode
    {
        public struct Entry
        {
            public Part Part;
            public bool IsNew, IsClone;
            public Entry(Part part, bool isNew, bool isClone) { Part = part; IsNew = isNew; IsClone = isClone; }
        }

        private sealed class Carried
        {
            public Part Part;
            public bool IsNew, IsClone;
            public HexIndex OrigPos;
            public HexRotation OrigRot;
            public int OrigLen;
            public List<HexIndex> OrigTrack;
            public HexIndex Offset; // part position minus cursor
        }

        private readonly EditorContext _ctx;
        private readonly List<Carried> _items = new List<Carried>();
        private Solution _solution;   // held for the carry: the parts must go back even if the editor closes
        private HexIndex _origCursor; // where it was picked up: a cancel puts the cursor back
        private Action _onPlaced;

        public CarryMode(EditorContext ctx) { _ctx = ctx; }

        public bool Active => _items.Count > 0;
        public bool IsGroup => _items.Count > 1;

        /// <summary>One part: a new or copied one is placed with its base at the cursor.</summary>
        public void Start(Part part, bool isNew, bool isClone)
        {
            if (part == null) return;
            if (isNew) part.SetHexPosAndUpdate(_ctx.Cursor);
            StartMany(new[] { new Entry(part, isNew, isClone) }, null);
        }

        /// <summary>Parts where they stand (new ones positioned by the caller). A null announcement
        /// says "carrying" and the part's name; <paramref name="onPlaced"/> runs when they land.</summary>
        public void StartMany(IList<Entry> entries, string announcement, Action onPlaced = null)
        {
            var ed = _ctx.Ed;
            if (ed == null || entries == null || entries.Count == 0) return;
            var solution = ed.GetSolution();
            _items.Clear();
            foreach (var e in entries)
            {
                var part = e.Part;
                _items.Add(new Carried
                {
                    Part = part,
                    IsNew = e.IsNew,
                    IsClone = e.IsClone,
                    OrigPos = part.GetHexPos(),
                    OrigRot = part.GetRotation(),
                    OrigLen = part.GetArmLength(),
                    OrigTrack = part.GetType().isTrack ? new List<HexIndex>(part.GetTrack()) : null,
                    Offset = part.GetHexPos() - _ctx.Cursor,
                });
                if (!e.IsNew) solution.RemovePart(part);
            }
            _origCursor = _ctx.Cursor;
            _solution = solution;
            _onPlaced = onPlaced;
            Assets.sounds.piece_pickup.PlaySound(1f);
            if (announcement != null) EditorContext.Say(announcement);
            else EditorContext.Say(Strings.EditorCarrying(Board.PartName(ed, entries[0].Part)), interrupt: !entries[0].IsNew);
        }

        private string PlacementError()
        {
            var solution = _ctx.Ed.GetSolution();
            foreach (var c in _items)
            {
                string message;
                var alt = c.IsNew ? c.Part.GetHexPos() : c.OrigPos;
                if (solution.IsAllowedPlacement(c.Part, alt, c.Part.GetHexPos(), c.Part.GetRotation(), out message)) continue;
                string error = EditorContext.Error(message);
                return IsGroup ? Strings.EditorPartError(Board.PartName(_ctx.Ed, c.Part), error) : error;
            }
            return null;
        }

        public void Move(HexDir dir)
        {
            var ed = _ctx.Ed;
            if (ed == null) return;
            _ctx.Cursor = Hex.Step(_ctx.Cursor, dir);
            foreach (var c in _items) c.Part.SetHexPosAndUpdate(_ctx.Cursor + c.Offset);
            string readout = Board.Readout(ed, _ctx.Cursor);
            string error = PlacementError();
            EditorContext.Say(error == null ? readout : readout + ", " + error);
        }

        public void Rotate(bool counterclockwise)
        {
            var ed = _ctx.Ed;
            var solution = ed.GetSolution();
            // One part turns only if it can; a group turns as a whole round the cursor (the game's).
            if (!IsGroup && !_items[0].Part.IsRotatable(solution)) return;
            var rot = counterclockwise ? HexRotation.Counterclockwise : HexRotation.Clockwise;
            foreach (var c in _items)
            {
                c.Part.SetHexPosAndUpdate(c.Part.GetHexPos().RotatedAround(_ctx.Cursor, rot));
                c.Part.RotateBy(solution, rot);
                c.Offset = c.Part.GetHexPos() - _ctx.Cursor;
            }
            Assets.sounds.piece_rotate.PlaySound(1f);
            EditorContext.Say(IsGroup ? Strings.EditorRotated : _ctx.Facing(_items[0].Part));
        }

        public void Length(int delta)
        {
            // The game changes the length of a lone part only.
            if (IsGroup) return;
            var part = _items[0].Part;
            if (!part.HasArms()) return;
            int before = part.GetArmLength();
            part.SetAllowedLength(before + delta);
            if (part.GetArmLength() == before) return;
            Assets.sounds.piece_modify.PlaySound(1f);
            EditorContext.Say(Strings.EditorLength(part.GetArmLength()));
        }

        /// <summary>Put the parts down; false (the reason spoken) where the game would not.</summary>
        public bool Drop()
        {
            var ed = _ctx.Ed;
            if (ed == null) return false;
            var solution = ed.GetSolution();
            string error = PlacementError();
            if (error != null) { EditorContext.Say(error); return false; }
            var items = new List<Carried>(_items);
            bool group = IsGroup;
            _items.Clear();
            foreach (var c in items) solution.RepositionPart(c.Part, c.Part.GetHexPos());
            // Copied arms are numbered right after the highest-numbered part carried, in order (the
            // game's drag-copy).
            EditorContext.NumberCopies(solution, items.Select(c => c.Part).ToList(), items.Where(c => c.IsClone).Select(c => c.Part));
            // A group's copies become the selection (the game's).
            if (group && items.Any(c => c.IsClone))
            {
                ed.ClearPartSelection();
                foreach (var c in items) ed.partSelection.Add(c.Part);
            }
            ed.MarkNeedsNewSnapshot();
            Assets.sounds.piece_place.PlaySound(1f);
            EditorContext.Say(Strings.EditorPlaced(_ctx.Names(items.Select(c => c.Part).ToList())));
            var onPlaced = _onPlaced;
            _onPlaced = null;
            onPlaced?.Invoke();
            // A new track is drawn hex by hex from where it was put down.
            if (!group && items[0].IsNew && items[0].Part.GetType().isTrack) _ctx.Draw.Start(items[0].Part);
            return true;
        }

        /// <summary>Discard the carried parts (Delete while carrying): placed ones are removed from
        /// the solution, new ones never existed.</summary>
        public void Delete()
        {
            var ed = _ctx.Ed;
            if (ed == null || _items.Count == 0) return;
            string names = _ctx.Names(_items.Select(c => c.Part).ToList());
            bool anyPlaced = _items.Any(c => !c.IsNew);
            foreach (var c in _items) ed.partSelection.Remove(c.Part);
            _items.Clear();
            _onPlaced = null;
            if (anyPlaced) ed.MarkNeedsNewSnapshot();
            Assets.sounds.piece_remove.PlaySound(1f);
            EditorContext.Say(Strings.EditorDeleted(names));
        }

        public void Cancel(bool speak = true)
        {
            if (_items.Count == 0) return;
            var items = new List<Carried>(_items);
            _items.Clear();
            _onPlaced = null;
            var solution = _solution;
            foreach (var c in items)
            {
                if (c.IsNew) continue;
                try
                {
                    var part = c.Part;
                    if (c.OrigTrack != null)
                    {
                        while (part.GetTrack().Count > 0) part.RemoveTrackBack();
                        foreach (var h in c.OrigTrack) part.AddTrackBack(h);
                    }
                    else if (solution != null) part.SetRotation(solution, c.OrigRot);
                    part.SetAllowedLength(c.OrigLen);
                    part.SetHexPosAndUpdate(c.OrigPos);
                    if (solution != null) solution.RepositionPart(part, c.OrigPos);
                    else CoreLog.Error("[editor] carry cancelled with no solution: " + part.GetType().id + " not restored");
                }
                catch (Exception e) { CoreLog.Error("[editor] restoring a carried part failed: " + e); }
            }
            _ctx.Cursor = _origCursor;
            if (speak) EditorContext.Say(Strings.EditorCancelled);
        }
    }

    /// <summary>
    /// A track drawn hex by hex from its end: a hex key extends it that way, or, pointing back along
    /// it, takes its last hex off. Enter finishes where the game's placement check allows, Escape
    /// restores the track as it was.
    /// </summary>
    internal sealed class TrackDrawMode
    {
        private readonly EditorContext _ctx;
        private Part _track;
        private List<HexIndex> _orig;

        public TrackDrawMode(EditorContext ctx) { _ctx = ctx; }

        public bool Active => _track != null;

        public void Start(Part track)
        {
            var ed = _ctx.Ed;
            if (ed == null || track == null || !track.GetType().isTrack) return;
            _track = track;
            _orig = new List<HexIndex>(track.GetTrack());
            _ctx.Cursor = End();
            EditorContext.Say(Strings.EditorDrawing(track.GetTrack().Count), interrupt: false);
        }

        private HexIndex End()
        {
            var hexes = _track.GetTrack();
            return _track.GetHexPos() + hexes[hexes.Count - 1];
        }

        public void Step(HexDir dir)
        {
            var ed = _ctx.Ed;
            if (ed == null) return;
            var hexes = _track.GetTrack();
            var target = Hex.Step(End(), dir) - _track.GetHexPos();
            if (hexes.Count >= 2 && hexes[hexes.Count - 2] == target) _track.RemoveTrackBack();
            else if (hexes.Contains(target)) return; // a track does not cross itself
            else _track.AddTrackBack(target);
            _ctx.Cursor = End();
            EditorContext.Say(Board.Readout(ed, _ctx.Cursor));
        }

        public bool Finish()
        {
            var ed = _ctx.Ed;
            if (ed == null) return false;
            var solution = ed.GetSolution();
            string message;
            if (!solution.IsAllowedPlacement(_track, _track.GetHexPos(), _track.GetHexPos(), _track.GetRotation(), out message))
            {
                EditorContext.Say(EditorContext.Error(message));
                return false;
            }
            int count = _track.GetTrack().Count;
            bool changed = !_orig.SequenceEqual(_track.GetTrack());
            _track = null;
            if (changed) ed.MarkNeedsNewSnapshot();
            Assets.sounds.piece_place.PlaySound(1f);
            EditorContext.Say(Strings.EditorTrackDone(count));
            return true;
        }

        public void Cancel(bool speak = true)
        {
            if (_track == null) return;
            var track = _track;
            _track = null;
            try
            {
                while (track.GetTrack().Count > 0) track.RemoveTrackBack();
                foreach (var h in _orig) track.AddTrackBack(h);
            }
            catch (Exception e) { CoreLog.Error("[editor] restoring a drawn track failed: " + e); }
            if (speak) EditorContext.Say(Strings.EditorCancelled);
        }
    }
}
