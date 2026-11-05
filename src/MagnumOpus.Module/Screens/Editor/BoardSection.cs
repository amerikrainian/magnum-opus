using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens.Editor
{
    /// <summary>
    /// The board stop: a hex cursor over the game's unbounded board (Civ V Access's cluster: Q
    /// northwest, E northeast, A west, D east, Z southwest, C southeast; numpad 7 9 4 6 1 3; the arrows
    /// are not board keys (user rule 2026-11-04); Alt with them skips to the next hex that reads
    /// differently; Alt+Shift on the letters, Alt on the numpad). A hex
    /// reads its half-step coordinates, then what is on it (<see cref="Board"/>).
    /// Reading: [ ] categories, , . items, S details, W the molecule, X neighbours, ; and ' the
    /// program cursor a cycle back and forward on the arm under the cursor, digits jump to an arm.
    /// Editing (stopped engine): Enter picks the part under the cursor up (<see cref="CarryMode"/>)
    /// and puts it down; Ctrl+Enter picks up a copy; Shift+A / Shift+D rotate, Shift+W / Shift+S
    /// lengthen and shorten an arm (the game's own drag letters); Delete removes the part;
    /// Shift+Enter on a track extends it (<see cref="TrackDrawMode"/>). Alt with an instruction key
    /// programs the arm under the cursor at the program cursor's cycle and steps that cursor on
    /// (user decision 2026-11-04: Ctrl is the clipboard). Alt+Backspace jumps to the tape on the arm
    /// under the cursor (and back, <see cref="ProgramSection"/>).
    /// Selection (user design 2026-11-04; the game's own partSelection, so the game draws it too):
    /// Shift+Space marks one corner, the next the opposite one and selects every part touching that
    /// rectangle of x, y (the game's box-select rule), a third starts over; Ctrl+Shift+Space adds or
    /// removes the part under the cursor (the game's Shift+click); Ctrl+A selects every part; Escape
    /// clears. With the cursor on a selected part, Enter / Ctrl+Enter / Delete / Shift+A / Shift+D /
    /// Ctrl+C / Ctrl+X take the whole group; Ctrl+V places the clipboard at the cursor.
    /// While carrying or drawing, the hex keys move the part or the track's end and Escape cancels.
    /// </summary>
    internal sealed class BoardSection : ScreenSection
    {
        private const int SkipLimit = 40;

        private readonly EditorContext _ctx;
        private string _category;
        private int _item = -1;

        public BoardSection(EditorContext ctx) { _ctx = ctx; }

        public override void OnPop() { _category = null; _item = -1; }

        public override void Build(GraphBuilder b)
        {
            var ed = _ctx.Ed;
            if (ed == null || ed.GetSolution() == null) return;
            b.BeginStop(EditorContext.StopBoard);
            b.PushContext(Strings.EditorBoard, null, positions: false);
            b.AddItem(ControlId.Structural("editor:board"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[] { Nodes.LabelPart(() => _ctx.Ed == null ? "" : Board.Readout(_ctx.Ed, _ctx.Cursor)) },
                OnActivate = Enter,
            });
            b.PopContext();
        }

        private bool Focused => Equals(Navigation.FocusedStopKey, EditorContext.StopBoard);

        public override IEnumerable<ElementAction> GetActions()
        {
            if (!Focused || _ctx.Ed == null) yield break;
            int arms = Board.Arms(_ctx.Ed).Count;

            yield return new ElementAction(EditorActions.HexNW, Strings.Get("bind.ed.hex.nw"), _ => Move(HexDir.NorthWest));
            yield return new ElementAction(EditorActions.HexNE, Strings.Get("bind.ed.hex.ne"), _ => Move(HexDir.NorthEast));
            yield return new ElementAction(EditorActions.HexW, Strings.Get("bind.ed.hex.w"), _ => Move(HexDir.West));
            yield return new ElementAction(EditorActions.HexE, Strings.Get("bind.ed.hex.e"), _ => Move(HexDir.East));
            yield return new ElementAction(EditorActions.HexSW, Strings.Get("bind.ed.hex.sw"), _ => Move(HexDir.SouthWest));
            yield return new ElementAction(EditorActions.HexSE, Strings.Get("bind.ed.hex.se"), _ => Move(HexDir.SouthEast));

            if (_ctx.Carry.Active)
            {
                yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ed.cancel"), _ => _ctx.Carry.Cancel());
                yield return new ElementAction(EditorActions.RotateCcw, Strings.Get("bind.ed.rot.ccw"), _ => _ctx.Carry.Rotate(true));
                yield return new ElementAction(EditorActions.RotateCw, Strings.Get("bind.ed.rot.cw"), _ => _ctx.Carry.Rotate(false));
                yield return new ElementAction(EditorActions.Longer, Strings.Get("bind.ed.len.up"), _ => _ctx.Carry.Length(1));
                yield return new ElementAction(EditorActions.Shorter, Strings.Get("bind.ed.len.down"), _ => _ctx.Carry.Length(-1));
                yield return new ElementAction(EditorActions.Delete, Strings.Get("bind.ed.delete"), _ => _ctx.Carry.Delete());
                yield break;
            }
            if (_ctx.Draw.Active)
            {
                yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ed.cancel"), _ => _ctx.Draw.Cancel());
                yield break;
            }

            yield return new ElementAction(EditorActions.SkipNW, Strings.Get("bind.ed.skip.nw"), _ => Skip(HexDir.NorthWest));
            yield return new ElementAction(EditorActions.SkipNE, Strings.Get("bind.ed.skip.ne"), _ => Skip(HexDir.NorthEast));
            yield return new ElementAction(EditorActions.SkipW, Strings.Get("bind.ed.skip.w"), _ => Skip(HexDir.West));
            yield return new ElementAction(EditorActions.SkipE, Strings.Get("bind.ed.skip.e"), _ => Skip(HexDir.East));
            yield return new ElementAction(EditorActions.SkipSW, Strings.Get("bind.ed.skip.sw"), _ => Skip(HexDir.SouthWest));
            yield return new ElementAction(EditorActions.SkipSE, Strings.Get("bind.ed.skip.se"), _ => Skip(HexDir.SouthEast));
            if (arms > 0)
            {
                yield return new ElementAction(EditorActions.TapePrev, Strings.Get("bind.ed.tape.prev"), _ => TapeFromBoard(-1));
                yield return new ElementAction(EditorActions.TapeNext, Strings.Get("bind.ed.tape.next"), _ => TapeFromBoard(1));
                yield return new ElementAction(EditorActions.Jump, Strings.Get("bind.ed.jump"), _ => JumpToTape());
                yield return new ElementAction(EditorActions.Instruction, Strings.Get("bind.ed.program_arm"), _ => ProgramArm());
            }
            for (int n = 1; n <= Math.Min(9, arms); n++)
            {
                int arm = n;
                yield return new ElementAction(EditorActions.ArmPrefix + n, Strings.Get("bind.ed.arm"), _ => JumpToArm(arm));
            }
            yield return new ElementAction(EditorActions.CatPrev, Strings.Get("bind.ed.cat.prev"), _ => Category(-1));
            yield return new ElementAction(EditorActions.CatNext, Strings.Get("bind.ed.cat.next"), _ => Category(1));
            yield return new ElementAction(EditorActions.ItemPrev, Strings.Get("bind.ed.item.prev"), _ => Item(-1));
            yield return new ElementAction(EditorActions.ItemNext, Strings.Get("bind.ed.item.next"), _ => Item(1));
            yield return new ElementAction(EditorActions.Details, Strings.Get("bind.ed.details"), _ => SayDetails());
            yield return new ElementAction(EditorActions.Info, Strings.Get("bind.ed.details"), _ => SayDetails());
            yield return new ElementAction(EditorActions.Molecule, Strings.Get("bind.ed.molecule"), _ => EditorContext.Say(Board.MoleculeAround(_ctx.Ed, _ctx.Cursor)));
            yield return new ElementAction(EditorActions.Neighbours, Strings.Get("bind.ed.neighbours"), _ => EditorContext.Say(NeighboursText()));

            // Selecting and the clipboard.
            yield return new ElementAction(EditorActions.MarkCorner, Strings.Get("bind.ed.mark"), _ => MarkCorner());
            yield return new ElementAction(EditorActions.ToggleSelect, Strings.Get("bind.ed.toggle_select"), _ => ToggleSelect());
            yield return new ElementAction(EditorActions.SelectParts, Strings.Get("bind.ed.select_parts"), _ => SelectAllParts());
            if (_ctx.PartsSelected)
                yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ed.unselect"), _ => ClearSelection());
            yield return new ElementAction(EditorActions.Copy, Strings.Get("bind.ed.copy"), _ => CopyParts(cut: false));
            yield return new ElementAction(EditorActions.Cut, Strings.Get("bind.ed.cut"), _ => CopyParts(cut: true));
            yield return new ElementAction(EditorActions.Paste, Strings.Get("bind.ed.paste"), _ => PasteParts());

            // Editing the part under the cursor.
            if (_ctx.PartAt(_ctx.Cursor) == null) yield break;
            yield return new ElementAction(EditorActions.RotateCcw, Strings.Get("bind.ed.rot.ccw"), _ => Rotate(true));
            yield return new ElementAction(EditorActions.RotateCw, Strings.Get("bind.ed.rot.cw"), _ => Rotate(false));
            yield return new ElementAction(EditorActions.Longer, Strings.Get("bind.ed.len.up"), _ => Length(1));
            yield return new ElementAction(EditorActions.Shorter, Strings.Get("bind.ed.len.down"), _ => Length(-1));
            yield return new ElementAction(EditorActions.Delete, Strings.Get("bind.ed.delete"), _ => Delete());
            yield return new ElementAction(EditorActions.Clone, Strings.Get("bind.ed.clone"), _ => Clone());
            yield return new ElementAction(EditorActions.DrawTrack, Strings.Get("bind.ed.track"), _ => ExtendTrack());
        }

        // ---- moving ----

        private void Move(HexDir dir)
        {
            if (_ctx.Carry.Active) { _ctx.Carry.Move(dir); return; }
            if (_ctx.Draw.Active) { _ctx.Draw.Step(dir); return; }
            _ctx.Cursor = Hex.Step(_ctx.Cursor, dir);
            SayHex();
        }

        private void SayHex()
        {
            var ed = _ctx.Ed;
            if (ed != null) EditorContext.Say(Board.Readout(ed, _ctx.Cursor));
        }

        // To the next hex that way whose contents read differently; nothing within reach: silent.
        private void Skip(HexDir dir)
        {
            var ed = _ctx.Ed;
            if (ed == null) return;
            var world = Board.WorldAtoms(ed);
            string here = string.Join("|", Board.Contents(ed, _ctx.Cursor, world));
            for (int i = 1; i <= SkipLimit; i++)
            {
                var h = Hex.Step(_ctx.Cursor, dir, i);
                if (string.Join("|", Board.Contents(ed, h, world)) == here) continue;
                _ctx.Cursor = h;
                SayHex();
                return;
            }
        }

        private void JumpToArm(int n)
        {
            var ed = _ctx.Ed;
            var arms = Board.Arms(ed);
            if (n < 1 || n > arms.Count) return;
            _ctx.Row = n - 1;
            _ctx.Cursor = Board.BaseHex(ed, arms[n - 1]);
            SayHex();
        }

        private void TapeFromBoard(int step)
        {
            var ed = _ctx.Ed;
            var arm = _ctx.ArmAt(_ctx.Cursor);
            if (arm != null) _ctx.Row = Board.Arms(ed).IndexOf(arm);
            int col = Math.Max(0, Math.Min(EditorContext.MaxColumn, _ctx.Col + step));
            if (col == _ctx.Col && arm == null) return;
            _ctx.Col = col;
            EditorContext.Say(ProgramSection.CellText(_ctx, withArm: true));
        }

        // ---- reading ----

        private void SayDetails() => EditorContext.Say(string.Join(" ", Board.Details(_ctx.Ed, _ctx.Cursor)));

        private string NeighboursText()
        {
            var ed = _ctx.Ed;
            var world = Board.WorldAtoms(ed);
            var parts = new List<string>();
            foreach (var dir in Hex.All)
            {
                var c = Board.Contents(ed, Hex.Step(_ctx.Cursor, dir), world);
                if (c.Count > 0) parts.Add(Strings.EditorNeighbour(Hex.Word(dir), string.Join(", ", c)));
            }
            return string.Join("; ", parts);
        }

        private List<KeyValuePair<string, List<HexIndex>>> Categories(SolutionEditorScreen ed)
        {
            var cats = new List<KeyValuePair<string, List<HexIndex>>>();
            var solution = ed.GetSolution();
            void Add(string key, IEnumerable<HexIndex> hexes)
            {
                var items = hexes.ToList();
                if (items.Count > 0) cats.Add(new KeyValuePair<string, List<HexIndex>>(key, items));
            }
            Add("reagents", solution.parts.Where(p => p.GetType().isInput).Select(p => p.GetHexPos()));
            Add("products", solution.parts.Where(p => p.GetType().IsOutput()).Select(p => p.GetHexPos()));
            Add("arms", Board.Arms(ed).Select(p => Board.BaseHex(ed, p)));
            Add("glyphs", solution.parts.Where(p => p.GetType().glyphHexes.Length > 0).Select(p => p.GetHexPos()));
            Add("tracks", solution.parts.Where(p => p.GetType().isTrack).Select(p => p.GetHexPos()));
            if (Board.Running(ed))
                Add("atoms", ed.GetSimulation().GetMolecules().Where(m => m.GetAtoms().Count > 0)
                    .Select(m => m.GetAtoms().Keys.OrderBy(h => h.R).ThenBy(h => h.Q).First()));
            return cats;
        }

        private void Category(int step)
        {
            var cats = Categories(_ctx.Ed);
            if (cats.Count == 0) return;
            int at = cats.FindIndex(c => c.Key == _category);
            at = at < 0 ? (step > 0 ? 0 : cats.Count - 1) : ((at + step) % cats.Count + cats.Count) % cats.Count;
            _category = cats[at].Key;
            _item = -1;
            EditorContext.Say(Strings.EditorCategory(Strings.EditorCategoryName(_category), cats[at].Value.Count));
        }

        private void Item(int step)
        {
            var cats = Categories(_ctx.Ed);
            if (cats.Count == 0) return;
            int at = cats.FindIndex(c => c.Key == _category);
            if (at < 0) { at = 0; _category = cats[0].Key; _item = -1; }
            var items = cats[at].Value;
            _item = _item < 0 ? (step > 0 ? 0 : items.Count - 1) : ((_item + step) % items.Count + items.Count) % items.Count;
            _ctx.Cursor = items[_item];
            SayHex();
        }

        // ---- editing ----

        // The part under the cursor if it may be edited now (the reason said when not), else null.
        private Part Editable()
        {
            var part = _ctx.PartAt(_ctx.Cursor);
            if (part == null || !_ctx.CanEdit()) return null;
            if (part.GetIsFixed() || !part.HasNoParent())
            {
                EditorContext.Say(Strings.EditorFixed(Board.PartName(_ctx.Ed, part)));
                return null;
            }
            return part;
        }

        private void Enter()
        {
            if (_ctx.Carry.Active) { _ctx.Carry.Drop(); return; }
            if (_ctx.Draw.Active) { _ctx.Draw.Finish(); return; }
            var group = Group();
            if (group != null)
            {
                if (group.Count == 0) return;
                _ctx.Carry.StartMany(group.Select(p => new CarryMode.Entry(p, false, false)).ToList(), Strings.EditorCarrying(_ctx.Names(group)));
                return;
            }
            var part = Editable();
            if (part != null) _ctx.Carry.Start(part, isNew: false, isClone: false);
        }

        // The selected group when the cursor is on one of its parts and editing is allowed now.
        private List<Part> Group()
        {
            var group = _ctx.GroupAtCursor();
            if (group == null || group.Count == 0) return null;
            return _ctx.CanEdit() ? group : new List<Part>();
        }

        private void Rotate(bool counterclockwise)
        {
            var group = Group();
            if (group != null) { if (group.Count > 0) RotateGroup(group, counterclockwise); return; }
            var part = Editable();
            if (part == null) return;
            var ed = _ctx.Ed;
            var solution = ed.GetSolution();
            if (!part.IsRotatable(solution)) return;
            var rot = counterclockwise ? HexRotation.Counterclockwise : HexRotation.Clockwise;
            string message;
            if (!solution.IsAllowedPlacement(part, part.GetHexPos(), part.GetHexPos(), (part.GetRotation() + rot).Clamped06(), out message))
            {
                EditorContext.Say(EditorContext.Error(message));
                return;
            }
            part.RotateBy(solution, rot);
            ed.MarkNeedsNewSnapshot();
            Assets.sounds.piece_rotate.PlaySound(1f);
            EditorContext.Say(_ctx.Facing(part));
        }

        private void Length(int delta)
        {
            var part = Editable();
            if (part == null || !part.HasArms()) return;
            var ed = _ctx.Ed;
            int before = part.GetArmLength();
            part.SetAllowedLength(before + delta);
            if (part.GetArmLength() == before) return;
            string message;
            if (!ed.GetSolution().CanArmBePlacedInChamber(part, part, out message))
            {
                part.SetAllowedLength(before);
                EditorContext.Say(EditorContext.Error(message));
                return;
            }
            ed.MarkNeedsNewSnapshot();
            Assets.sounds.piece_modify.PlaySound(1f);
            EditorContext.Say(Strings.EditorLength(part.GetArmLength()));
        }

        private void Delete()
        {
            var group = Group();
            if (group != null) { if (group.Count > 0) DeleteParts(group); return; }
            var part = Editable();
            if (part == null || part.GetType().isConduit) return;
            var ed = _ctx.Ed;
            string name = Board.PartName(ed, part);
            ed.GetSolution().RemovePart(part);
            ed.MarkNeedsNewSnapshot();
            Assets.sounds.piece_remove.PlaySound(1f);
            EditorContext.Say(Strings.EditorDeleted(name));
        }

        // The game copies what it may (not a reagent, product, unique part or conduit, nor in a
        // puzzle that limits part counts); a copied program loses its period override.
        private void Clone()
        {
            var group = Group();
            if (group != null) { if (group.Count > 0) CloneGroup(group); return; }
            var part = Editable();
            if (part == null) return;
            var ed = _ctx.Ed;
            var solution = ed.GetSolution();
            var type = part.GetType();
            if (type.isInput || type.IsOutput() || type.isUnique || type.isConduit || solution.GetPuzzle().limitedPartAmounts.Count > 0)
            {
                EditorContext.Say(Strings.EditorCannotCopy(Board.PartName(ed, part)));
                return;
            }
            var copy = part.Clone(solution, MaybeHelper.empty);
            copy.program.RemoveOverride();
            _ctx.Carry.Start(copy, isNew: true, isClone: true);
        }

        private void ExtendTrack()
        {
            var part = Editable();
            if (part == null) return;
            if (!part.GetType().isTrack)
            {
                // An arm on a track: the track under it.
                var track = _ctx.Ed.GetSolution().GetTrackUnderPart(part);
                if (!track.HasValue()) return;
                part = track.GetValue();
            }
            _ctx.Draw.Start(part);
        }

        // Alt + an instruction key: the arm under the cursor gets it at the program cursor's cycle,
        // and that cursor steps on.
        private void ProgramArm()
        {
            if (!SdlKeyboard.AltHeld || SdlKeyboard.CtrlHeld || SdlKeyboard.ShiftHeld) return;
            var type = ProgramEdits.PressedInstruction();
            if (type == null) return;
            var arm = _ctx.ArmAt(_ctx.Cursor);
            if (arm == null) { EditorContext.Say(Strings.EditorNoArm); return; }
            if (!_ctx.CanEdit()) return;
            _ctx.Row = Board.Arms(_ctx.Ed).IndexOf(arm);
            ProgramSection.PlaceAtCursor(_ctx, type, insert: false, withPlace: true);
        }

        // ---- the tape jump ----

        // Alt+Backspace: the tape on the arm under the cursor, at the program cursor's column; the
        // same key there comes back to this hex.
        private void JumpToTape()
        {
            var ed = _ctx.Ed;
            var arm = _ctx.ArmAt(_ctx.Cursor);
            if (arm == null) { EditorContext.Say(Strings.EditorNoArm); return; }
            _ctx.Row = Board.Arms(ed).IndexOf(arm);
            _ctx.JumpHex = _ctx.Cursor;
            Navigation.JumpToStop(EditorContext.StopProgram);
        }

        // ---- selecting ----

        // Shift+Space: the first corner, then the opposite one (the parts touching the rectangle
        // become the selection), then starting over.
        private void MarkCorner()
        {
            var ed = _ctx.Ed;
            if (_ctx.Busy() || !_ctx.CanEdit()) return;
            if (!_ctx.Corner.HasValue)
            {
                _ctx.Corner = _ctx.Cursor;
                EditorContext.Say(Strings.EditorCorner(Hex.Coords(_ctx.Cursor)));
                return;
            }
            var a = _ctx.Corner.Value;
            var b = _ctx.Cursor;
            _ctx.Corner = null;
            var parts = PartsTouching(ed, a, b);
            ed.ClearPartSelection();
            foreach (var p in parts) ed.partSelection.Add(p);
            var selected = parts.Where(p => ed.partSelection.IsSelected(p)).ToList();
            string range = Strings.EditorRange(Hex.Coords(MinCorner(a, b)), Hex.Coords(MaxCorner(a, b)));
            EditorContext.Say(Strings.EditorMarked(range, selected.Count == 0 ? Strings.EditorNoParts : _ctx.Names(selected)));
        }

        // The rectangle in the spoken coordinates: doubled x (2Q + R, so half steps stay whole) and y.
        private static int X2(HexIndex h) => 2 * h.Q + h.R;

        private static HexIndex FromX2(int x2, int r) => new HexIndex((x2 - r) / 2, r);

        // Top-left and bottom-right as hexes for speech (a corner off the hex lattice rounds west).
        private static HexIndex MinCorner(HexIndex a, HexIndex b)
        {
            int r = Math.Max(a.R, b.R), x2 = Math.Min(X2(a), X2(b));
            if (((x2 - r) & 1) != 0) x2++;
            return FromX2(x2, r);
        }

        private static HexIndex MaxCorner(HexIndex a, HexIndex b)
        {
            int r = Math.Min(a.R, b.R), x2 = Math.Max(X2(a), X2(b));
            if (((x2 - r) & 1) != 0) x2--;
            return FromX2(x2, r);
        }

        // The game's box-select: a part is in if any of its hexes, a gripper or an attached atom
        // lies inside.
        private static List<Part> PartsTouching(SolutionEditorScreen ed, HexIndex a, HexIndex b)
        {
            int x0 = Math.Min(X2(a), X2(b)), x1 = Math.Max(X2(a), X2(b));
            int r0 = Math.Min(a.R, b.R), r1 = Math.Max(a.R, b.R);
            bool Inside(HexIndex h) => X2(h) >= x0 && X2(h) <= x1 && h.R >= r0 && h.R <= r1;
            var solution = ed.GetSolution();
            var result = new List<Part>();
            foreach (var part in solution.parts)
            {
                bool hit = part.GetHexFootprint(solution).Any(Inside)
                    || Board.Grippers(ed, part).Any(g => Inside(g.Key))
                    || part.GetType().attachedAtoms.Keys.Any(k => Inside(part.GetHexPos() + k));
                if (hit) result.Add(part);
            }
            return result;
        }

        // Ctrl+Shift+Space: the part under the cursor in or out (the game's Shift+click).
        private void ToggleSelect()
        {
            var ed = _ctx.Ed;
            var part = _ctx.PartAt(_ctx.Cursor);
            if (part == null || _ctx.Busy() || !_ctx.CanEdit()) return;
            var root = part.GetRootPart();
            if (root.GetIsFixed()) { EditorContext.Say(Strings.EditorFixed(_ctx.Label(root))); return; }
            if (ed.partSelection.IsSelected(root))
            {
                ed.partSelection.Remove(root);
                EditorContext.Say(Strings.EditorPartUnselected(_ctx.Label(root)));
            }
            else
            {
                ed.partSelection.Add(root);
                EditorContext.Say(Strings.EditorPartSelected(_ctx.Label(root)));
            }
        }

        // Ctrl+A: every part (the game's; fixed parts stay out).
        private void SelectAllParts()
        {
            var ed = _ctx.Ed;
            if (_ctx.Busy() || !_ctx.CanEdit()) return;
            _ctx.Corner = null;
            ed.ClearPartSelection();
            foreach (var p in ed.GetSolution().parts) ed.partSelection.Add(p);
            int n = ed.partSelection.GetCount();
            EditorContext.Say(n == 0 ? Strings.EditorNoParts : Strings.EditorPartSelected(Strings.EditorPartCount(n)));
        }

        private void ClearSelection()
        {
            _ctx.Corner = null;
            _ctx.Ed?.ClearPartSelection();
            EditorContext.Say(Strings.EditorSelectionCleared);
        }

        // ---- group edits ----

        // Turned in place round the cursor hex: every part must fit where it lands, else nothing moves.
        private void RotateGroup(List<Part> group, bool counterclockwise)
        {
            var ed = _ctx.Ed;
            var solution = ed.GetSolution();
            var rot = counterclockwise ? HexRotation.Counterclockwise : HexRotation.Clockwise;
            var before = group.Select(p => new { Part = p, Pos = p.GetHexPos(), Rot = p.GetRotation() }).ToList();
            foreach (var p in group) solution.RemovePart(p);
            foreach (var p in group)
            {
                p.SetHexPosAndUpdate(p.GetHexPos().RotatedAround(_ctx.Cursor, rot));
                p.RotateBy(solution, rot);
            }
            string error = null;
            foreach (var b in before)
            {
                string message;
                if (solution.IsAllowedPlacement(b.Part, b.Pos, b.Part.GetHexPos(), b.Part.GetRotation(), out message)) continue;
                error = Strings.EditorPartError(Board.PartName(ed, b.Part), EditorContext.Error(message));
                break;
            }
            if (error != null)
            {
                foreach (var b in before)
                {
                    b.Part.SetRotation(solution, b.Rot);
                    solution.RepositionPart(b.Part, b.Pos);
                }
                EditorContext.Say(error);
                return;
            }
            foreach (var p in group) solution.RepositionPart(p, p.GetHexPos());
            ed.MarkNeedsNewSnapshot();
            Assets.sounds.piece_rotate.PlaySound(1f);
            EditorContext.Say(Strings.EditorRotated);
        }

        // Removed together (conduits stay: the game never deletes them).
        private void DeleteParts(List<Part> parts)
        {
            var ed = _ctx.Ed;
            var gone = parts.Where(p => !p.GetType().isConduit && !p.GetIsFixed()).ToList();
            if (gone.Count == 0) return;
            string names = _ctx.Names(gone);
            foreach (var p in gone)
            {
                ed.partSelection.Remove(p);
                ed.GetSolution().RemovePart(p);
            }
            ed.MarkNeedsNewSnapshot();
            Assets.sounds.piece_remove.PlaySound(1f);
            EditorContext.Say(Strings.EditorDeleted(names));
        }

        // The game's drag-copy of a selection: what it may copy is copied, the rest moves along.
        private void CloneGroup(List<Part> group)
        {
            var ed = _ctx.Ed;
            var solution = ed.GetSolution();
            var copies = group.Where(_ctx.Copyable).ToList();
            if (copies.Count == 0) { EditorContext.Say(Strings.EditorCannotCopy(_ctx.Names(group))); return; }
            var moved = group.Where(p => !_ctx.Copyable(p)).ToList();
            var entries = new List<CarryMode.Entry>();
            foreach (var p in copies)
            {
                var copy = p.Clone(solution, MaybeHelper.empty);
                copy.program.RemoveOverride();
                entries.Add(new CarryMode.Entry(copy, true, true));
            }
            foreach (var p in moved) entries.Add(new CarryMode.Entry(p, false, false));
            string text = Strings.EditorCarryingCopies(_ctx.Names(copies));
            if (moved.Count > 0) text += ", " + Strings.EditorAlsoMoving(_ctx.Names(moved));
            _ctx.Carry.StartMany(entries, text);
        }

        // ---- the parts clipboard ----

        // Ctrl+C / Ctrl+X: the group under the cursor, else the part there, remembered at its offset
        // from the cursor. Copy keeps what the game may copy; cut takes the parts off the board.
        private void CopyParts(bool cut)
        {
            var ed = _ctx.Ed;
            if (ed == null || _ctx.Busy() || !_ctx.CanEdit()) return;
            var parts = _ctx.GroupAtCursor();
            if (parts == null)
            {
                var here = _ctx.PartAt(_ctx.Cursor);
                if (here == null) return;
                parts = new List<Part> { here.GetRootPart() };
            }
            parts = parts.Where(p => !p.GetIsFixed() && !p.GetType().isConduit).ToList();
            if (parts.Count == 0) return;
            var solution = ed.GetSolution();
            var copyable = parts.Where(_ctx.Copyable).ToList();
            if (!cut && copyable.Count == 0) { EditorContext.Say(Strings.EditorCannotCopy(_ctx.Names(parts))); return; }
            string names = _ctx.Names(cut ? parts : copyable);
            _ctx.PartClipboard.Clear();
            _ctx.CutParts.Clear();
            foreach (var p in copyable)
            {
                var template = p.Clone(solution, MaybeHelper.empty);
                template.program.RemoveOverride();
                _ctx.PartClipboard.Add(new KeyValuePair<Part, HexIndex>(template, p.GetHexPos() - _ctx.Cursor));
            }
            if (cut)
            {
                foreach (var p in parts)
                {
                    _ctx.CutParts.Add(new KeyValuePair<Part, HexIndex>(p, p.GetHexPos() - _ctx.Cursor));
                    ed.partSelection.Remove(p);
                    solution.RemovePart(p);
                }
                ed.MarkNeedsNewSnapshot();
                Assets.sounds.piece_remove.PlaySound(1f);
                EditorContext.Say(Strings.EditorCut(names));
                return;
            }
            if (parts.Count > copyable.Count)
                names += ", " + Strings.EditorCannotCopy(_ctx.Names(parts.Where(p => !_ctx.Copyable(p)).ToList()));
            EditorContext.Say(Strings.EditorCopied(names));
        }

        // What the parts tray would refuse for parts coming onto the board: a reagent or product
        // already placed, a unique part already there, a puzzle's part limit (the game enforces these
        // only in the tray, so a cut part pasted back must face them too). Null when all may land.
        private string TrayRefusal(List<Part> parts)
        {
            var ed = _ctx.Ed;
            var solution = ed.GetSolution();
            var puzzle = solution.GetPuzzle();
            var placed = solution.CollectPartsAndSubparts().ToList();
            foreach (var group in parts.GroupBy(p => p.GetType()))
            {
                var type = group.Key;
                string name = Board.PartName(ed, group.First());
                if (type.isInput || type.IsOutput())
                {
                    foreach (var p in group)
                    {
                        int index = p.GetInputOutputIndex();
                        if (solution.parts.Any(q => q.GetType().isInput == type.isInput && q.GetType().IsOutput() == type.IsOutput() && q.GetInputOutputIndex() == index))
                            return Strings.EditorPartError(Board.PartName(ed, p), Strings.EditorLimitReached);
                    }
                    continue;
                }
                int limit = type.isUnique ? 1 : (puzzle.limitedPartAmounts.TryGetValue(type, out int l) ? l : int.MaxValue);
                if (limit != int.MaxValue && placed.Count(q => q.GetType() == type) + group.Count() > limit)
                    return Strings.EditorPartError(name, Strings.EditorLimitReached);
            }
            return null;
        }

        // Ctrl+V: the clipboard placed at the cursor at once (user decision 2026-11-04), one undo
        // step; where anything does not fit, the reason is spoken and nothing lands. The first paste
        // after a cut places the cut parts themselves; every other paste places fresh copies.
        private void PasteParts()
        {
            var ed = _ctx.Ed;
            if (ed == null || _ctx.Busy() || !_ctx.CanEdit()) return;
            var solution = ed.GetSolution();
            var entries = new List<CarryMode.Entry>();
            bool moving = _ctx.CutParts.Count > 0;
            if (moving)
            {
                foreach (var kv in _ctx.CutParts)
                {
                    kv.Key.SetHexPosAndUpdate(_ctx.Cursor + kv.Value);
                    entries.Add(new CarryMode.Entry(kv.Key, true, false));
                }
            }
            else
            {
                foreach (var kv in _ctx.PartClipboard)
                {
                    if (!_ctx.Copyable(kv.Key)) continue;
                    var copy = kv.Key.Clone(solution, MaybeHelper.empty);
                    copy.SetHexPosAndUpdate(_ctx.Cursor + kv.Value);
                    entries.Add(new CarryMode.Entry(copy, true, true));
                }
            }
            if (entries.Count == 0) return;
            var parts = entries.Select(e => e.Part).ToList();
            string refusal = TrayRefusal(parts);
            if (refusal != null) { EditorContext.Say(refusal); return; }
            // All or nothing: every part must fit where it lands (the game's placement check).
            foreach (var p in parts)
            {
                string message;
                if (solution.IsAllowedPlacement(p, p.GetHexPos(), p.GetHexPos(), p.GetRotation(), out message)) continue;
                string error = EditorContext.Error(message);
                EditorContext.Say(parts.Count > 1 ? Strings.EditorPartError(Board.PartName(ed, p), error) : error);
                return;
            }
            foreach (var p in parts) solution.RepositionPart(p, p.GetHexPos());
            EditorContext.NumberCopies(solution, parts, entries.Where(e => e.IsClone).Select(e => e.Part));
            if (moving) _ctx.CutParts.Clear();
            // Several parts land selected, so Enter on one of them moves them all.
            if (parts.Count > 1)
            {
                ed.ClearPartSelection();
                foreach (var p in parts) ed.partSelection.Add(p);
            }
            ed.MarkNeedsNewSnapshot();
            Assets.sounds.piece_place.PlaySound(1f);
            EditorContext.Say(Strings.EditorPasted(_ctx.Names(parts)));
        }
    }
}
