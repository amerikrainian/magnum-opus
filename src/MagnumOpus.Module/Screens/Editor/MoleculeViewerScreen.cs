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
    /// W reads the summary; Escape closes. The molecule is fetched from the puzzle each time it is
    /// read, never held.
    /// </summary>
    internal sealed class MoleculeViewerScreen : Screen
    {
        private readonly string _title;
        private readonly Func<Molecule> _molecule;
        private HexIndex _cursor;

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
                if (m != null) EditorContext.Say(Board.MoleculeSummaryText(m));
            });
        }

        private void Move(HexDir dir)
        {
            _cursor = Hex.Step(_cursor, dir);
            EditorContext.Say(Readout());
        }

        private string Readout()
        {
            var parts = new List<string> { Hex.Coords(_cursor) };
            var m = _molecule();
            if (m != null && m.GetAtoms().TryGetValue(_cursor, out var atom))
            {
                parts.Add(Board.AtomName(atom.atomType));
                string bonds = Board.Bonds(new Board.AtomAt { Atom = atom, Molecule = m, Hex = _cursor });
                if (bonds.Length > 0) parts.Add(bonds);
            }
            return string.Join(", ", parts);
        }
    }
}
