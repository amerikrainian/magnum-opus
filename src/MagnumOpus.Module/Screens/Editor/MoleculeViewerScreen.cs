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
    /// A reagent's or product's molecule as the parts tray draws it on its tile, before it is placed
    /// (W on the tile; a child screen of the editor, nothing placed or bought). The hex keys walk it as
    /// the board is walked: each hex reads its coordinates in the molecule's own frame, then its atom
    /// and the directions of its bonds ("1, 0, Water, bonds west"); an empty hex reads its coordinates.
    /// X reads the neighbours, as on the board. Shift+A / Shift+D turn the molecule round the cursor
    /// hex, as the game's A / D do while dragging: only this viewer's view of it (a turned copy, the
    /// puzzle's molecule untouched), gone when the viewer closes. W reads the summary; Escape closes.
    /// The molecule is fetched from the puzzle each time it is read, never held.
    /// </summary>
    internal sealed class MoleculeViewerScreen : Screen
    {
        private readonly string _title;
        private readonly Func<Molecule> _molecule;
        private HexIndex _cursor;

        // The view: the molecule turned round the origin, then moved. No game object.
        private HexRotation _rotation = HexRotation.R0;
        private HexIndex _offset = new HexIndex(0, 0);

        public MoleculeViewerScreen(string title, Func<Molecule> molecule)
        {
            _title = title;
            _molecule = molecule;
            var m = molecule();
            // Start on the molecule's origin when an atom is there, else on its top-left atom.
            if (m != null && m.GetAtoms().Count > 0 && !m.GetAtoms().ContainsKey(new HexIndex(0, 0)))
                _cursor = m.GetAtoms().Keys.OrderByDescending(h => h.R).ThenBy(h => 2 * h.Q + h.R).First();
            else _cursor = new HexIndex(0, 0);
        }

        public static void Open(string title, Func<Molecule> molecule) =>
            ScreenManager.Current?.PushChild(new MoleculeViewerScreen(title, molecule));

        public override string Key => "editor.molecule_viewer";
        public override bool IsActive() => false; // a child screen only
        public override string ScreenName => _title;
        public override bool AllowsTypeahead => false;

        public override void Build(GraphBuilder b)
        {
            b.AddItem(ControlId.Structural("molecule:cursor"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[] { Nodes.LabelPart(Readout) },
            });
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ParentScreen?.RemoveChild(this));
            yield return new ElementAction(EditorActions.HexNW, Strings.Get("bind.ed.hex.nw"), _ => Move(HexDir.NorthWest));
            yield return new ElementAction(EditorActions.HexNE, Strings.Get("bind.ed.hex.ne"), _ => Move(HexDir.NorthEast));
            yield return new ElementAction(EditorActions.HexW, Strings.Get("bind.ed.hex.w"), _ => Move(HexDir.West));
            yield return new ElementAction(EditorActions.HexE, Strings.Get("bind.ed.hex.e"), _ => Move(HexDir.East));
            yield return new ElementAction(EditorActions.HexSW, Strings.Get("bind.ed.hex.sw"), _ => Move(HexDir.SouthWest));
            yield return new ElementAction(EditorActions.HexSE, Strings.Get("bind.ed.hex.se"), _ => Move(HexDir.SouthEast));
            yield return new ElementAction(EditorActions.Molecule, Strings.Get("bind.ed.molecule"), _ =>
            {
                var m = _molecule();
                if (m != null) EditorContext.Say(Board.MoleculeSummaryText(m, _rotation.GetNumberOfTurns()));
            });
            yield return new ElementAction(EditorActions.Neighbours, Strings.Get("bind.ed.neighbours"), _ => EditorContext.Say(Neighbours()));
            yield return new ElementAction(EditorActions.RotateCcw, Strings.Get("bind.ed.rot.ccw"), _ => Rotate(HexRotation.Counterclockwise));
            yield return new ElementAction(EditorActions.RotateCw, Strings.Get("bind.ed.rot.cw"), _ => Rotate(HexRotation.Clockwise));
        }

        // Round the cursor hex, so what is under the cursor stays there.
        private void Rotate(HexRotation by)
        {
            _rotation = (_rotation + by).Clamped06();
            _offset = _offset.RotatedAround(_cursor, by);
            Assets.sounds.piece_rotate.PlaySound(1f);
            EditorContext.Say(Hex.Word(Hex.FromTurns(_rotation.GetNumberOfTurns()))); // the board's feedback for a molecule
            EditorContext.Say(Readout(), interrupt: false);
        }

        // A copy as the viewer shows it; null when the puzzle has no molecule here.
        private Molecule View()
        {
            var m = _molecule();
            if (m == null) return null;
            var view = m.Clone();
            view.Rotate(new HexIndex(0, 0), _rotation);
            view.Translate(_offset);
            return view;
        }

        private void Move(HexDir dir)
        {
            _cursor = Hex.Step(_cursor, dir);
            EditorContext.Say(Readout());
        }

        private string Readout()
        {
            string atom = Describe(View(), _cursor);
            return atom == null ? Hex.Coords(_cursor) : Hex.Coords(_cursor) + ", " + atom;
        }

        private string Neighbours()
        {
            var m = View();
            var parts = new List<string>();
            foreach (var dir in Hex.All)
            {
                string atom = Describe(m, Hex.Step(_cursor, dir));
                if (atom != null) parts.Add(Strings.EditorNeighbour(Hex.Word(dir), atom));
            }
            return string.Join("; ", parts);
        }

        // The atom on a hex and the directions of its bonds; null for an empty hex.
        private static string Describe(Molecule m, HexIndex hex)
        {
            if (m == null || !m.GetAtoms().TryGetValue(hex, out var atom)) return null;
            string bonds = Board.Bonds(new Board.AtomAt { Atom = atom, Molecule = m, Hex = hex });
            return bonds.Length > 0 ? Board.AtomName(atom.atomType) + ", " + bonds : Board.AtomName(atom.atomType);
        }
    }
}
