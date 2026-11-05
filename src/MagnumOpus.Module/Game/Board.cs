using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Strings;

namespace MagnumOpus.Module.Game
{
    /// <summary>
    /// The solution editor's board read live for speech: parts and their footprints, arms with their
    /// grippers, and atoms (the running simulation's molecules; while stopped, the reagents and
    /// products drawn on their parts). Nothing is kept across frames: every query reads the editor.
    /// Arms are numbered as the program panel numbers its rows (the solution's ordered
    /// programmables), so "arm 1" on the board is row 1 of the tape.
    /// </summary>
    public static class Board
    {
        public static Solution SolutionOf(SolutionEditorScreen ed) => ed?.GetSolution();

        public static bool Running(SolutionEditorScreen ed) => ed.GetSimPlayState() != SimPlayState.Stopped;

        /// <summary>The programmable parts in tape order (row 1 first).</summary>
        /// <summary>The programmable parts in tape order (row 1 first): the game's own ordering (by
        /// program index, as Solution.OrderProgrammables sorts them) applied to the parts in the
        /// solution now. The solution's cached list refreshes only when it saves, so it can hold a
        /// part just picked up or miss one just put down.</summary>
        public static List<Part> Arms(SolutionEditorScreen ed)
        {
            var solution = SolutionOf(ed);
            if (solution == null) return new List<Part>();
            return solution.parts.Where(p => p.GetType().isProgrammable).OrderBy(p => p.programIndex).ToList();
        }

        /// <summary>1-based tape row of a part, or 0 when it is not programmable or not on the board.</summary>
        public static int ArmNumber(SolutionEditorScreen ed, Part part)
        {
            var arms = Arms(ed);
            if (part == null) return 0;
            var root = part.GetRootPart();
            int i = arms.IndexOf(root);
            return i < 0 ? 0 : i + 1;
        }

        /// <summary>The part's name as the game's tooltip gives it ("Reagent: Water" when the
        /// molecule has a name; "Fixed-Length Arm (2 grippers)", see <see cref="TypeName"/>).</summary>
        public static string PartName(SolutionEditorScreen ed, Part part)
        {
            var type = part.GetType();
            string name = TypeName(type);
            if (type.isInput || type.IsOutput())
            {
                var molecule = part.GetMoleculeForInputOutput(SolutionOf(ed));
                if (molecule != null && molecule.displayName.HasValue())
                    name = GameText.Clean(string.Format(UIConsts.inputOutputNameFormat, name, molecule.displayName.GetValue().GetTranslated()));
            }
            return name;
        }

        /// <summary>A part type's name. The game calls the one-, two-, three- and six-armed assemblies
        /// all "Fixed-Length Arm" and tells them apart by their picture, so a multi-armed one carries
        /// its gripper count: "Fixed-Length Arm (2 grippers)".</summary>
        public static string TypeName(PartType type)
        {
            string name = GameText.Of(type.name);
            int arms = type.armRotations?.Length ?? 0;
            return arms > 1 ? Strings.EditorWithGrippers(name, arms) : name;
        }

        public static string AtomName(AtomType type) => ReferenceEquals(type, null) ? "" : GameText.Of(type.name);

        /// <summary>The base hex of a part right now (an arm on a track moves while running).</summary>
        public static HexIndex BaseHex(SolutionEditorScreen ed, Part part) => ed.GetSimulation().GetSimState(part).newPosition;

        public static int Turns(SolutionEditorScreen ed, Part part) => ed.GetSimulation().GetSimState(part).rotation.GetNumberOfTurns();

        public static int Length(SolutionEditorScreen ed, Part part)
        {
            var type = part.GetType();
            return type.canExtend ? ed.GetSimulation().GetSimState(part).length : part.GetArmLength();
        }

        /// <summary>Every gripper hex of an arm right now, with its gripper subpart.</summary>
        public static List<KeyValuePair<HexIndex, Part>> Grippers(SolutionEditorScreen ed, Part arm)
        {
            var result = new List<KeyValuePair<HexIndex, Part>>();
            if (!arm.HasArms()) return result;
            var sim = ed.GetSimulation();
            for (int i = 0; i < arm.GetType().armRotations.Length; i++)
            {
                var grip = arm.subparts != null && i < arm.subparts.Length ? arm.subparts[i] : null;
                result.Add(new KeyValuePair<HexIndex, Part>(sim.GetGripperHex(arm, i), grip));
            }
            return result;
        }

        /// <summary>The hexes a part covers right now. An arm covers its base hex (its grippers are
        /// reported separately); everything else, its footprint.</summary>
        public static IEnumerable<HexIndex> Footprint(SolutionEditorScreen ed, Part part)
        {
            if (part.GetType().isProgrammable) return new[] { BaseHex(ed, part) };
            return part.GetHexFootprint(SolutionOf(ed));
        }

        /// <summary>An atom on the board with the molecule it belongs to.</summary>
        public struct AtomAt
        {
            public Atom Atom;
            public Molecule Molecule;
            public HexIndex Hex;
            public Part IoPart; // set for a reagent or product drawn while stopped
        }

        /// <summary>The atoms on the board by hex: the simulation's molecules while running. While
        /// stopped there are none in play (the reagents and products are read with their parts).</summary>
        public static Dictionary<HexIndex, AtomAt> WorldAtoms(SolutionEditorScreen ed)
        {
            var map = new Dictionary<HexIndex, AtomAt>();
            if (!Running(ed)) return map;
            foreach (var m in ed.GetSimulation().GetMolecules())
                foreach (var kv in m.GetAtoms())
                    map[kv.Key] = new AtomAt { Atom = kv.Value, Molecule = m, Hex = kv.Key };
            return map;
        }

        /// <summary>The atom of a reagent or product part drawn on this hex (the puzzle's molecule
        /// placed with the part's position and rotation), or null.</summary>
        public static AtomAt? IoAtom(SolutionEditorScreen ed, Part part, HexIndex hex)
        {
            var type = part.GetType();
            if (!type.isInput && !type.IsOutput()) return null;
            var molecule = part.GetMoleculeForInputOutput(SolutionOf(ed));
            if (molecule == null) return null;
            var local = (hex - part.GetHexPos()).Rotated(new HexRotation(-part.GetRotation().GetNumberOfTurns()));
            if (!molecule.GetAtoms().TryGetValue(local, out var atom)) return null;
            return new AtomAt { Atom = atom, Molecule = molecule, Hex = local, IoPart = part };
        }

        /// <summary>The bonds of an atom as directions ("bonds east, northeast"), empty for none. For
        /// a reagent or product atom the molecule is in the part's local frame: its directions are
        /// turned by the part's rotation.</summary>
        public static string Bonds(AtomAt at)
        {
            int turn = at.IoPart != null ? at.IoPart.GetRotation().GetNumberOfTurns() : 0;
            var plain = new List<string>();
            var triplex = new List<string>();
            foreach (var bond in at.Molecule.GetBonds())
            {
                HexIndex other;
                if (bond.hexPos1 == at.Hex) other = bond.hexPos2;
                else if (bond.hexPos2 == at.Hex) other = bond.hexPos1;
                else continue;
                var dir = Hex.DirOf(other.Q - at.Hex.Q, other.R - at.Hex.R);
                if (dir == null) continue;
                string word = Hex.Word(Hex.FromTurns((int)dir.Value + turn));
                if ((bond.type & BondTypeEnum.Standard) != 0) plain.Add(word);
                else triplex.Add(word);
            }
            var parts = new List<string>();
            if (plain.Count > 0) parts.Add(Strings.EditorBonds(string.Join(", ", plain)));
            if (triplex.Count > 0) parts.Add(Strings.EditorTriplex(string.Join(", ", triplex)));
            return string.Join(", ", parts);
        }

        /// <summary>The atom's own words: name, then bonds, then who holds it.</summary>
        public static string DescribeAtom(SolutionEditorScreen ed, AtomAt at)
        {
            var parts = new List<string> { AtomName(at.Atom.atomType) };
            string bonds = Bonds(at);
            if (bonds.Length > 0) parts.Add(bonds);
            if (at.IoPart == null)
            {
                var holder = ed.GetHolder(at.Molecule);
                if (holder.HasValue())
                {
                    int n = ArmNumber(ed, holder.GetValue());
                    if (n > 0) parts.Add(Strings.EditorHeldBy(n));
                }
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// What is on a hex, distinguishing word first, coordinates excluded: the atom in play, arm
        /// bases, grippers, then the other parts (glyphs, tracks, reagents and products with the atom
        /// they place here). Empty for an empty hex.
        /// </summary>
        public static List<string> Contents(SolutionEditorScreen ed, HexIndex hex, Dictionary<HexIndex, AtomAt> world = null)
        {
            var result = new List<string>();
            var solution = SolutionOf(ed);
            if (solution == null) return result;
            world = world ?? WorldAtoms(ed);
            if (world.TryGetValue(hex, out var atom)) result.Add(DescribeAtom(ed, atom));

            var arms = Arms(ed);
            for (int i = 0; i < arms.Count; i++)
            {
                var arm = arms[i];
                if (BaseHex(ed, arm) == hex) result.Add(DescribeArmBase(ed, arm, i + 1));
            }
            for (int i = 0; i < arms.Count; i++)
            {
                foreach (var grip in Grippers(ed, arms[i]))
                {
                    if (grip.Key != hex) continue;
                    bool closed = Running(ed) && grip.Value != null && ed.GetSimulation().GetSimState(grip.Value).grabState;
                    result.Add(closed ? Strings.EditorGripClosed(i + 1) : Strings.EditorGrip(i + 1));
                }
            }
            foreach (var part in solution.parts)
            {
                var type = part.GetType();
                if (type.isProgrammable) continue;
                if (!part.GetHexFootprint(solution).Contains(hex)) continue;
                string name = PartName(ed, part);
                if (type.isTrack) name = TrackHere(part, hex, name);
                var io = IoAtom(ed, part, hex);
                if (io.HasValue)
                {
                    // While stopped the part's atom is the one on the hex. While running, the atoms in
                    // play are read first and a product's outline (the game draws it throughout) reads
                    // as what it wants, so it cannot pass for an atom.
                    if (!Running(ed)) name += ", " + DescribeAtom(ed, io.Value);
                    else if (type.IsOutput()) name += ", " + Strings.EditorProductWants(DescribeAtom(ed, io.Value));
                }
                result.Add(name);
            }
            return result;
        }

        /// <summary>A track hex: the directions it joins in, each marked with the way a move takes it
        /// there: "Track, +northeast, -southeast" (track plus goes to the next hex as drawn, minus to
        /// the previous; an end joins one way; a loop wraps). The game shows the same thing: a minus
        /// sign on the end drawn first, a plus on the last, both at a loop's seam (user decision
        /// 2026-11-04).</summary>
        private static string TrackHere(Part track, HexIndex hex, string name)
        {
            var hexes = track.GetTrack();
            var origin = track.GetHexPos();
            int i = -1;
            for (int k = 0; k < hexes.Count; k++) if (origin + hexes[k] == hex) { i = k; break; }
            if (i < 0 || hexes.Count < 2) return name;
            bool loop = track.IsTrackLooping();
            var parts = new List<string> { name };
            int next = i + 1 < hexes.Count ? i + 1 : (loop ? 0 : -1);
            int prev = i > 0 ? i - 1 : (loop ? hexes.Count - 1 : -1);
            if (next >= 0)
            {
                var d = Hex.DirOf(hexes[next].Q - hexes[i].Q, hexes[next].R - hexes[i].R);
                if (d.HasValue) parts.Add(Strings.EditorTrackPlus(Hex.Word(d.Value)));
            }
            if (prev >= 0)
            {
                var d = Hex.DirOf(hexes[prev].Q - hexes[i].Q, hexes[prev].R - hexes[i].R);
                if (d.HasValue) parts.Add(Strings.EditorTrackMinus(Hex.Word(d.Value)));
            }
            return string.Join(", ", parts);
        }

        public static string DescribeArmBase(SolutionEditorScreen ed, Part arm, int number) =>
            Strings.EditorArmBase(number, PartName(ed, arm), Hex.Word(Hex.FromTurns(Turns(ed, arm))), Length(ed, arm));

        /// <summary>The hex's full readout: "selected" when a selected part is on it, coordinates,
        /// then its contents.</summary>
        public static string Readout(SolutionEditorScreen ed, HexIndex hex, string extra = null)
        {
            var parts = new List<string>();
            if (HasSelectedPart(ed, hex)) parts.Add(Strings.EditorHexSelected);
            parts.Add(Hex.Coords(hex));
            if (extra != null) parts.Add(extra);
            parts.AddRange(Contents(ed, hex));
            return string.Join(", ", parts);
        }

        /// <summary>Whether a part in the game's selection covers the hex (an arm by its base or a
        /// gripper).</summary>
        public static bool HasSelectedPart(SolutionEditorScreen ed, HexIndex hex)
        {
            var selection = ed.partSelection;
            var solution = SolutionOf(ed);
            if (selection == null || solution == null || selection.GetCount() == 0) return false;
            foreach (var part in solution.parts)
            {
                if (!selection.IsSelected(part)) continue;
                if (part.GetType().isProgrammable)
                {
                    if (BaseHex(ed, part) == hex || Grippers(ed, part).Any(g => g.Key == hex)) return true;
                }
                else if (part.GetHexFootprint(solution).Contains(hex)) return true;
            }
            return false;
        }

        /// <summary>The descriptions the game gives for what is on a hex (its tooltips).</summary>
        public static List<string> Details(SolutionEditorScreen ed, HexIndex hex)
        {
            var result = new List<string>();
            var solution = SolutionOf(ed);
            if (solution == null) return result;
            var seen = new HashSet<PartType>();
            foreach (var part in solution.parts)
            {
                bool here = part.GetType().isProgrammable
                    ? BaseHex(ed, part) == hex || Grippers(ed, part).Any(g => g.Key == hex)
                    : part.GetHexFootprint(solution).Contains(hex);
                if (!here || !seen.Add(part.GetType())) continue;
                string d = GameText.Of(part.GetType().description);
                if (d.Length > 0) result.Add(PartName(ed, part) + ". " + d);
            }
            return result;
        }

        /// <summary>The molecule touching a hex (in play, else a reagent's or product's) in brief: its
        /// size, what it is made of and its bonds, then who holds it ("3 atoms: 2 Fire, Salt; 2 bonds;
        /// held by arm 2"). A product or reagent leads with its name. The layout is read hex by hex.</summary>
        public static string MoleculeAround(SolutionEditorScreen ed, HexIndex hex)
        {
            var world = WorldAtoms(ed);
            if (world.TryGetValue(hex, out var at))
            {
                var parts = MoleculeSummary(at.Molecule);
                var holder = ed.GetHolder(at.Molecule);
                if (holder.HasValue())
                {
                    int n = ArmNumber(ed, holder.GetValue());
                    if (n > 0) parts.Add(Strings.EditorHeldBy(n));
                }
                return string.Join("; ", parts);
            }
            var solution = SolutionOf(ed);
            if (solution == null) return null;
            foreach (var part in solution.parts)
            {
                var io = IoAtom(ed, part, hex);
                if (!io.HasValue) continue;
                return PartName(ed, part) + ", " + string.Join("; ", MoleculeSummary(io.Value.Molecule));
            }
            return null;
        }

        // Size and composition (most common atom first, then the molecule's own order), then the
        // bond counts. A single atom is just its name.
        private static List<string> MoleculeSummary(Molecule molecule)
        {
            var atoms = molecule.GetAtoms().Values.Select(a => AtomName(a.atomType)).ToList();
            var groups = atoms.GroupBy(n => n).OrderByDescending(g => g.Count()).ThenBy(g => atoms.IndexOf(g.Key));
            string composition = string.Join(", ", groups.Select(g => g.Count() == 1 ? g.Key : Strings.EditorAtomCount(g.Count(), g.Key)));
            var parts = new List<string> { atoms.Count == 1 ? composition : Strings.EditorMoleculeAtoms(atoms.Count, composition) };
            int plain = 0, triplex = 0;
            foreach (var bond in molecule.GetBonds())
            {
                if ((bond.type & BondTypeEnum.Standard) != 0) plain++;
                else triplex++;
            }
            if (plain > 0) parts.Add(Strings.EditorMoleculeBonds(plain));
            if (triplex > 0) parts.Add(Strings.EditorMoleculeTriplex(triplex));
            return parts;
        }
    }
}
