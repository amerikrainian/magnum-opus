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
    /// differently). A hex
    /// reads its half-step coordinates, then what is on it (<see cref="Board"/>).
    /// Reading: [ ] categories, , . items, S details, W the molecule, X neighbours, ; and ' the
    /// program cursor a cycle back and forward on the arm under the cursor, digits jump to an arm.
    /// Editing (stopped engine): Enter picks the part under the cursor up (<see cref="CarryMode"/>)
    /// and puts it down; Ctrl+Enter picks up a copy; Shift+A / Shift+D rotate, Shift+W / Shift+S
    /// lengthen and shorten an arm (the game's own drag letters); Delete removes the part;
    /// Shift+Enter on a track extends it (<see cref="TrackDrawMode"/>); Ctrl with an instruction key
    /// programs the arm under the cursor at the program cursor's cycle and steps that cursor on.
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
            var part = Editable();
            if (part != null) _ctx.Carry.Start(part, isNew: false, isClone: false);
        }

        private void Rotate(bool counterclockwise)
        {
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

        // Ctrl + an instruction key: the arm under the cursor gets it at the program cursor's cycle.
        private void ProgramArm()
        {
            if (!SdlKeyboard.CtrlHeld || SdlKeyboard.ShiftHeld || SdlKeyboard.AltHeld) return;
            var type = ProgramEdits.PressedInstruction();
            if (type == null) return;
            var ed = _ctx.Ed;
            var arm = _ctx.ArmAt(_ctx.Cursor);
            if (arm == null) { EditorContext.Say(Strings.EditorNoArm); return; }
            if (!_ctx.CanEdit()) return;
            int row = Board.Arms(ed).IndexOf(arm);
            if (!ProgramEdits.Allowed(ed, arm, row + 1, type)) return;
            _ctx.Row = row;
            int col = _ctx.Col;
            ProgramEdits.Place(arm, col, type, insert: false);
            _ctx.Col = Math.Min(EditorContext.MaxColumn, col + 1);
            EditorContext.Say(Strings.EditorCell(col + 1, Instructions.Label(type)));
        }
    }
}
