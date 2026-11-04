using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
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

        // ---- modes ----
        public readonly CarryMode Carry;
        public readonly TrackDrawMode Draw;

        /// <summary>A mode Escape ends (so the game does not get it).</summary>
        public bool InMode => Carry.Active || Draw.Active || Selecting;

        public void Reset()
        {
            Cursor = new HexIndex(0, 0);
            Row = 0;
            Col = 0;
            Selecting = false;
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
    }

    /// <summary>
    /// A part picked up on the board (the game's drag, by keyboard): out of the solution while
    /// carried, moved with the hex keys (the hex it was taken by stays under the cursor), rotated
    /// round the cursor, put down with Enter where the game's own placement check allows (its reason
    /// spoken where it does not), cancelled with Escape (back where it was, or discarded when new).
    /// </summary>
    internal sealed class CarryMode
    {
        private readonly EditorContext _ctx;
        private Part _part;
        private Solution _solution; // held for the carry: the part must go back even if the editor closes
        private bool _isNew, _isClone;
        private HexIndex _origPos;
        private HexRotation _origRot;
        private int _origLen;
        private List<HexIndex> _origTrack;
        private HexIndex _offset;     // part position minus cursor
        private HexIndex _origCursor; // where it was picked up: a cancel puts the cursor back

        public CarryMode(EditorContext ctx) { _ctx = ctx; }

        public bool Active => _part != null;
        public Part Part => _part;

        public void Start(Part part, bool isNew, bool isClone)
        {
            var ed = _ctx.Ed;
            if (ed == null || part == null) return;
            var solution = ed.GetSolution();
            _isNew = isNew;
            _isClone = isClone;
            _origPos = part.GetHexPos();
            _origRot = part.GetRotation();
            _origLen = part.GetArmLength();
            _origTrack = part.GetType().isTrack ? new List<HexIndex>(part.GetTrack()) : null;
            if (isNew) part.SetHexPosAndUpdate(_ctx.Cursor);
            else solution.RemovePart(part);
            _offset = part.GetHexPos() - _ctx.Cursor;
            _origCursor = _ctx.Cursor;
            _part = part;
            _solution = solution;
            Assets.sounds.piece_pickup.PlaySound(1f);
            EditorContext.Say(Strings.EditorCarrying(Board.PartName(ed, part)), interrupt: !isNew);
        }

        private string PlacementError()
        {
            var ed = _ctx.Ed;
            var solution = ed.GetSolution();
            string message;
            var alt = _isNew ? _part.GetHexPos() : _origPos;
            if (!solution.IsAllowedPlacement(_part, alt, _part.GetHexPos(), _part.GetRotation(), out message))
                return EditorContext.Error(message);
            return null;
        }

        public void Move(HexDir dir)
        {
            var ed = _ctx.Ed;
            if (ed == null) return;
            _ctx.Cursor = Hex.Step(_ctx.Cursor, dir);
            _part.SetHexPosAndUpdate(_ctx.Cursor + _offset);
            string readout = Board.Readout(ed, _ctx.Cursor);
            string error = PlacementError();
            EditorContext.Say(error == null ? readout : readout + ", " + error);
        }

        public void Rotate(bool counterclockwise)
        {
            var ed = _ctx.Ed;
            var solution = ed.GetSolution();
            if (!_part.IsRotatable(solution)) return;
            var rot = counterclockwise ? HexRotation.Counterclockwise : HexRotation.Clockwise;
            _part.SetHexPosAndUpdate(_part.GetHexPos().RotatedAround(_ctx.Cursor, rot));
            _part.RotateBy(solution, rot);
            _offset = _part.GetHexPos() - _ctx.Cursor;
            Assets.sounds.piece_rotate.PlaySound(1f);
            EditorContext.Say(_ctx.Facing(_part));
        }

        public void Length(int delta)
        {
            if (!_part.HasArms()) return;
            int before = _part.GetArmLength();
            _part.SetAllowedLength(before + delta);
            if (_part.GetArmLength() == before) return;
            Assets.sounds.piece_modify.PlaySound(1f);
            EditorContext.Say(Strings.EditorLength(_part.GetArmLength()));
        }

        /// <summary>Put the part down; false (the reason spoken) where the game would not.</summary>
        public bool Drop()
        {
            var ed = _ctx.Ed;
            if (ed == null) return false;
            var solution = ed.GetSolution();
            string error = PlacementError();
            if (error != null) { EditorContext.Say(error); return false; }
            var part = _part;
            solution.RepositionPart(part, part.GetHexPos());
            if (_isClone && part.GetType().isProgrammable)
            {
                // The game numbers a clone after every other arm.
                int next = solution.parts.Where(p => p.GetType().isProgrammable && p != part).Select(p => p.programIndex).DefaultIfEmpty(-1).Max() + 1;
                solution.SetProgramIndex(part, next);
            }
            ed.MarkNeedsNewSnapshot();
            bool newTrack = _isNew && part.GetType().isTrack;
            _part = null;
            Assets.sounds.piece_place.PlaySound(1f);
            EditorContext.Say(Strings.EditorPlaced(Board.PartName(ed, part)));
            // A new track is drawn hex by hex from where it was put down.
            if (newTrack) _ctx.Draw.Start(part);
            return true;
        }

        /// <summary>Discard the carried part (Delete while carrying): a placed one is removed from
        /// the solution, a new one never existed.</summary>
        public void Delete()
        {
            var ed = _ctx.Ed;
            if (ed == null || _part == null) return;
            string name = Board.PartName(ed, _part);
            bool wasPlaced = !_isNew;
            _part = null;
            if (wasPlaced) ed.MarkNeedsNewSnapshot();
            Assets.sounds.piece_remove.PlaySound(1f);
            EditorContext.Say(Strings.EditorDeleted(name));
        }

        public void Cancel(bool speak = true)
        {
            if (_part == null) return;
            var part = _part;
            _part = null;
            try
            {
                if (!_isNew)
                {
                    var solution = _solution;
                    if (_origTrack != null)
                    {
                        while (part.GetTrack().Count > 0) part.RemoveTrackBack();
                        foreach (var h in _origTrack) part.AddTrackBack(h);
                    }
                    else if (solution != null) part.SetRotation(solution, _origRot);
                    part.SetAllowedLength(_origLen);
                    part.SetHexPosAndUpdate(_origPos);
                    if (solution != null) solution.RepositionPart(part, _origPos);
                    else CoreLog.Error("[editor] carry cancelled with no solution: " + part.GetType().id + " not restored");
                }
                _ctx.Cursor = _origCursor;
            }
            catch (Exception e) { CoreLog.Error("[editor] restoring a carried part failed: " + e); }
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
