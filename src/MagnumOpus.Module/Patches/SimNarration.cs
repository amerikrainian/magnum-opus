using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MagnumOpus.Core;
using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Game;

namespace MagnumOpus.Module.Patches
{
    /// <summary>
    /// What happens in each cycle of the editor's running simulation, worded as what happened, one
    /// sentence per actor (SpeechChem's rule): "arm 1: grabbed Fire, clockwise".
    ///
    /// A cycle is Sim.BeginCycle (instructions, glyphs at cycle start, movement, inputs) then
    /// Sim.EndCycle (glyphs at cycle end). The arms' moves are read from their sim states right
    /// after BeginCycle (the game stores each cycle's deltas there); the glyphs' work is the
    /// difference of the board's atoms and bonds across each RunCycleGlyphs call (atoms do not move
    /// inside one), attributed to the part covering the hex; products are the outputDone counters
    /// across the cycle. Content mods' glyphs are covered by the same difference. Only the
    /// simulation the editor is showing is narrated (the program panel builds throwaway ones every
    /// frame to check the program).
    ///
    /// Every cycle goes to the run log (<see cref="RunLog"/>). Speech, one message per event: every
    /// event while stepping, after "cycle N"; while playing, each cycle's first message cuts off the
    /// last cycle's (no backlog at speed); in fast forward, products only. Errors are spoken by the editor screen when the game shows them.
    /// </summary>
    internal static class SimNarration
    {
        private sealed class CycleRecord
        {
            public int Number;
            public readonly Dictionary<int, List<string>> Arms = new Dictionary<int, List<string>>();
            public readonly List<string> Others = new List<string>();
            public readonly List<string> Products = new List<string>();

            public void Arm(int n, string what)
            {
                if (!Arms.TryGetValue(n, out var list)) Arms[n] = list = new List<string>();
                list.Add(what);
            }
        }

        private struct WorldAtom { public AtomType Type; }

        private static Sim _sim;           // the simulation the log belongs to
        private static CycleRecord _cycle; // the cycle being recorded
        private static readonly Dictionary<Part, string> _heldBefore = new Dictionary<Part, string>();
        private static readonly Dictionary<Part, bool> _haltedBefore = new Dictionary<Part, bool>();
        private static readonly Dictionary<Part, int> _outputsBefore = new Dictionary<Part, int>();
        private static Dictionary<HexIndex, WorldAtom> _atomsBefore;
        private static Dictionary<string, BondTypeEnum> _bondsBefore;

        public static void Apply(Harmony harmony)
        {
            harmony.Patch(Expr.MethodOf(() => default(Sim).BeginCycle()),
                prefix: new HarmonyMethod(typeof(SimNarration), nameof(BeginPrefix)),
                postfix: new HarmonyMethod(typeof(SimNarration), nameof(BeginPostfix)));
            harmony.Patch(Expr.MethodOf(() => default(Sim).EndCycle()),
                postfix: new HarmonyMethod(typeof(SimNarration), nameof(EndPostfix)));
            harmony.Patch(Expr.MethodOf(() => default(Sim).RunCycleGlyphs(false)),
                prefix: new HarmonyMethod(typeof(SimNarration), nameof(GlyphsPrefix)),
                postfix: new HarmonyMethod(typeof(SimNarration), nameof(GlyphsPostfix)));
            CoreLog.Info("[sim] simulation narration armed");
        }

        private static SolutionEditorScreen LiveEditor(Sim sim)
        {
            var ed = GameScreens.Find<SolutionEditorScreen>();
            if (ed == null || ed.GetSimPlayState() == SimPlayState.Stopped) return null;
            return ReferenceEquals(ed.GetSimulation(), sim) ? ed : null;
        }

        // ---- BeginCycle: open the record, remember what the arms held ----

        private static void BeginPrefix(Sim __instance)
        {
            try
            {
                var ed = LiveEditor(__instance);
                if (ed == null)
                {
                    // Another simulation (a check, a recording) leaves the editor's record alone.
                    if (ReferenceEquals(_sim, __instance)) _cycle = null;
                    return;
                }
                if (!ReferenceEquals(_sim, __instance))
                {
                    _sim = __instance;
                    RunLog.Clear(); // a new run
                }
                _cycle = new CycleRecord { Number = __instance.GetCycle() + 1 };
                _heldBefore.Clear();
                _haltedBefore.Clear();
                _outputsBefore.Clear();
                foreach (var arm in Board.Arms(ed))
                {
                    _haltedBefore[arm] = ((ISim)__instance).GetSimState(arm).hasHalted;
                    if (arm.subparts == null) continue;
                    foreach (var grip in arm.subparts)
                    {
                        var held = ((ISim)__instance).GetSimState(grip).heldMolecule;
                        if (held.HasValue()) _heldBefore[grip] = DescribeMolecule(held.GetValue());
                    }
                }
                foreach (var part in ed.GetSolution().parts)
                    if (part.GetType().IsOutput()) _outputsBefore[part] = ((ISim)__instance).GetSimState(part).outputDone;
            }
            catch (Exception e) { CoreLog.Warning("[sim] cycle start: " + e.Message); _cycle = null; }
        }

        // ---- BeginCycle done: the arms' moves this cycle ----

        private static void BeginPostfix(Sim __instance)
        {
            if (_cycle == null || !ReferenceEquals(_sim, __instance)) return;
            try
            {
                var ed = LiveEditor(__instance);
                if (ed == null) return;
                var arms = Board.Arms(ed);
                for (int i = 0; i < arms.Count; i++) RecordArm(__instance, arms[i], i + 1);
            }
            catch (Exception e) { CoreLog.Warning("[sim] arm moves: " + e.Message); }
        }

        private static void RecordArm(Sim sim, Part arm, int n)
        {
            var s = ((ISim)sim).GetSimState(arm);
            // Grippers first: what an arm picks up or lets go is the news of the cycle.
            var grabbed = new List<string>();
            var dropped = new List<string>();
            bool grabbedNothing = false, droppedNothing = false;
            if (arm.subparts != null)
            {
                var seen = new HashSet<Molecule>();
                foreach (var grip in arm.subparts)
                {
                    var g = ((ISim)sim).GetSimState(grip);
                    if (!g.hasGrabStateChanged) continue;
                    if (g.grabState)
                    {
                        if (g.heldMolecule.HasValue())
                        {
                            if (seen.Add(g.heldMolecule.GetValue())) grabbed.Add(DescribeMolecule(g.heldMolecule.GetValue()));
                        }
                        else grabbedNothing = true;
                    }
                    else if (_heldBefore.TryGetValue(grip, out var was)) { if (!dropped.Contains(was)) dropped.Add(was); }
                    else droppedNothing = true;
                }
            }
            if (grabbed.Count > 0) _cycle.Arm(n, Strings.EventGrabbed(string.Join(", ", grabbed)));
            else if (grabbedNothing) _cycle.Arm(n, Strings.EventGrabbedNothing);
            if (dropped.Count > 0) _cycle.Arm(n, Strings.EventDropped(string.Join(", ", dropped)));
            else if (droppedNothing) _cycle.Arm(n, Strings.EventDroppedNothing);

            int turns = s.deltaRotation.GetNumberOfTurns();
            if (turns != 0) _cycle.Arm(n, Repeat(turns > 0 ? Strings.EventCounter : Strings.EventClockwise, Math.Abs(turns)));
            int pivot = s.deltaPivot.GetNumberOfTurns();
            if (pivot != 0) _cycle.Arm(n, Repeat(pivot > 0 ? Strings.EventPivotCounter : Strings.EventPivotClockwise, Math.Abs(pivot)));
            if (s.deltaLength > 0) _cycle.Arm(n, Strings.EventExtended);
            else if (s.deltaLength < 0) _cycle.Arm(n, Strings.EventRetracted);
            if (s.deltaPosition.Q != 0 || s.deltaPosition.R != 0)
            {
                var dir = Hex.DirOf(s.deltaPosition.Q, s.deltaPosition.R);
                _cycle.Arm(n, Strings.EventMoved(dir.HasValue ? Hex.Word(dir.Value) : Hex.Describe(s.deltaPosition.Q, s.deltaPosition.R)));
            }
            if (s.hasHalted && !(_haltedBefore.TryGetValue(arm, out var h) && h)) _cycle.Arm(n, Strings.EventHalted);
        }

        private static string Repeat(string word, int times) => times == 1 ? word : Strings.EventTimes(word, times);

        // ---- glyphs: the board's atoms and bonds before and after ----

        private static void GlyphsPrefix(Sim __instance)
        {
            if (_cycle == null || !ReferenceEquals(_sim, __instance)) { _atomsBefore = null; return; }
            try { Snapshot(__instance, out _atomsBefore, out _bondsBefore); }
            catch (Exception e) { CoreLog.Warning("[sim] glyph snapshot: " + e.Message); _atomsBefore = null; }
        }

        private static void GlyphsPostfix(Sim __instance)
        {
            if (_cycle == null || _atomsBefore == null || !ReferenceEquals(_sim, __instance)) return;
            try
            {
                var ed = LiveEditor(__instance);
                if (ed == null) return;
                Snapshot(__instance, out var atoms, out var bonds);
                var events = new List<KeyValuePair<HexIndex, string>>();
                foreach (var kv in atoms)
                {
                    if (!_atomsBefore.TryGetValue(kv.Key, out var was))
                        events.Add(new KeyValuePair<HexIndex, string>(kv.Key, Strings.EventAppeared(Board.AtomName(kv.Value.Type))));
                    else if (was.Type != kv.Value.Type)
                        events.Add(new KeyValuePair<HexIndex, string>(kv.Key, Strings.EventTransmuted(Board.AtomName(was.Type), Board.AtomName(kv.Value.Type))));
                }
                foreach (var kv in _atomsBefore)
                    if (!atoms.ContainsKey(kv.Key))
                        events.Add(new KeyValuePair<HexIndex, string>(kv.Key, Strings.EventRemoved(Board.AtomName(kv.Value.Type))));
                foreach (var kv in bonds)
                    if (!_bondsBefore.ContainsKey(kv.Key) && ParseBond(kv.Key, out var a, out var b))
                        events.Add(new KeyValuePair<HexIndex, string>(a, Strings.EventBonded(AtomAt(atoms, a), AtomAt(atoms, b))));
                foreach (var kv in _bondsBefore)
                    if (!bonds.ContainsKey(kv.Key) && ParseBond(kv.Key, out var a, out var b))
                        events.Add(new KeyValuePair<HexIndex, string>(a, Strings.EventUnbonded(AtomAt(_atomsBefore, a), AtomAt(_atomsBefore, b))));
                // One sentence per part that did the work (two reagents of one name are two parts),
                // in the order it happened; work on no part is keyed by its hex. A product taking its
                // molecule is said by its count at the cycle's end, not atom by atom here.
                var byPart = new List<KeyValuePair<object, List<string>>>();
                var names = new Dictionary<object, string>();
                foreach (var ev in events)
                {
                    if (OnProduct(ed, ev.Key)) continue;
                    var part = PartAt(ed, ev.Key);
                    object key = (object)part ?? Hex.Coords(ev.Key);
                    if (!names.ContainsKey(key)) names[key] = part != null ? Board.PartName(ed, part) : Hex.Coords(ev.Key);
                    var entry = byPart.FirstOrDefault(p => Equals(p.Key, key));
                    if (entry.Value == null) { entry = new KeyValuePair<object, List<string>>(key, new List<string>()); byPart.Add(entry); }
                    entry.Value.Add(ev.Value);
                }
                foreach (var p in byPart) _cycle.Others.Add(Strings.EventActor(names[p.Key], string.Join(", ", p.Value)));
            }
            catch (Exception e) { CoreLog.Warning("[sim] glyph events: " + e.Message); }
            finally { _atomsBefore = null; }
        }

        private static void Snapshot(Sim sim, out Dictionary<HexIndex, WorldAtom> atoms, out Dictionary<string, BondTypeEnum> bonds)
        {
            atoms = new Dictionary<HexIndex, WorldAtom>();
            bonds = new Dictionary<string, BondTypeEnum>();
            foreach (var m in ((ISim)sim).GetMolecules())
            {
                foreach (var kv in m.GetAtoms()) atoms[kv.Key] = new WorldAtom { Type = kv.Value.atomType };
                foreach (var bond in m.GetBonds()) bonds[BondKey(bond.hexPos1, bond.hexPos2)] = bond.type;
            }
        }

        // An unordered pair of hexes as a key.
        private static string BondKey(HexIndex a, HexIndex b)
        {
            bool swap = a.Q > b.Q || (a.Q == b.Q && a.R > b.R);
            var x = swap ? b : a;
            var y = swap ? a : b;
            return x.Q + "," + x.R + "|" + y.Q + "," + y.R;
        }

        private static bool ParseBond(string key, out HexIndex a, out HexIndex b)
        {
            a = b = default(HexIndex);
            var halves = key.Split('|');
            if (halves.Length != 2) return false;
            var p = halves[0].Split(',');
            var q = halves[1].Split(',');
            a = new HexIndex(int.Parse(p[0]), int.Parse(p[1]));
            b = new HexIndex(int.Parse(q[0]), int.Parse(q[1]));
            return true;
        }

        private static string AtomAt(Dictionary<HexIndex, WorldAtom> atoms, HexIndex h) =>
            atoms.TryGetValue(h, out var a) ? Strings.EventAtomAt(Board.AtomName(a.Type), Hex.Coords(h)) : Hex.Coords(h);

        // The non-arm part covering a hex (a glyph, a reagent, a product), by name.
        private static Part PartAt(SolutionEditorScreen ed, HexIndex h)
        {
            var solution = ed.GetSolution();
            foreach (var part in solution.parts)
            {
                if (part.GetType().isProgrammable || part.GetType().isTrack) continue;
                if (part.GetHexFootprint(solution).Contains(h)) return part;
            }
            return null;
        }

        private static bool OnProduct(SolutionEditorScreen ed, HexIndex h)
        {
            var solution = ed.GetSolution();
            foreach (var part in solution.parts)
                if (part.GetType().IsOutput() && part.GetHexFootprint(solution).Contains(h)) return true;
            return false;
        }

        // ---- EndCycle: products, then the cycle goes out ----

        private static void EndPostfix(Sim __instance)
        {
            if (_cycle == null || !ReferenceEquals(_sim, __instance)) return;
            var cycle = _cycle;
            _cycle = null;
            try
            {
                var ed = LiveEditor(__instance);
                if (ed == null) return;
                foreach (var part in ed.GetSolution().parts)
                {
                    if (!part.GetType().IsOutput()) continue;
                    int done = ((ISim)__instance).GetSimState(part).outputDone;
                    _outputsBefore.TryGetValue(part, out int before);
                    // Up to the count the puzzle asks for: past it the game keeps counting (while other
                    // products are unfinished), which would repeat "6 of 6".
                    if (done > before && before < part.GetOutputCount())
                        cycle.Products.Add(Strings.EventProduct(Board.PartName(ed, part), Math.Min(done, part.GetOutputCount()), part.GetOutputCount()));
                }
                Flush(ed, cycle);
            }
            catch (Exception e) { CoreLog.Warning("[sim] cycle end: " + e.Message); }
        }

        private static void Flush(SolutionEditorScreen ed, CycleRecord cycle)
        {
            var sentences = new List<string>();
            foreach (var kv in cycle.Arms.OrderBy(k => k.Key))
                sentences.Add(Strings.EventArm(kv.Key, string.Join(", ", kv.Value)));
            sentences.AddRange(cycle.Others);
            foreach (var s in sentences) RunLog.Add(cycle.Number, s);
            foreach (var s in cycle.Products) RunLog.Add(cycle.Number, s);

            var state = ed.GetSimPlayState();
            bool fast = ed.isFastForward;
            // Fast forward is silent (user rule 2026-11-04); the run log and Alt+P keep the products.
            if (state == SimPlayState.Playing && fast) return;
            // One message per event, SpeechChem's way ("cycle 3", "arm 1: grabbed Fire", "arm 2: ..."):
            // the first interrupts, the rest queue behind it. A step opens with the cycle's number;
            // at speed each cycle's first message cuts the last cycle's off, so what is said is the
            // present and nothing backs up.
            var all = new List<string>(sentences);
            all.AddRange(cycle.Products);
            if (state != SimPlayState.Playing) all.Insert(0, Strings.EditorCycle(cycle.Number));
            for (int i = 0; i < all.Count; i++) Speech.Say(all[i], interrupt: i == 0);
        }

        /// <summary>A molecule in words: its atoms by name with counts ("Fire", "2 Fire, Salt").</summary>
        public static string DescribeMolecule(Molecule m)
        {
            var counts = new List<KeyValuePair<string, int>>();
            foreach (var kv in m.GetAtoms())
            {
                string name = Board.AtomName(kv.Value.atomType);
                int i = counts.FindIndex(c => c.Key == name);
                if (i < 0) counts.Add(new KeyValuePair<string, int>(name, 1));
                else counts[i] = new KeyValuePair<string, int>(name, counts[i].Value + 1);
            }
            return string.Join(", ", counts.Select(c => c.Value == 1 ? c.Key : Strings.EventCount(c.Value, c.Key)));
        }
    }

    /// <summary>The current run's events, oldest first, for the run log stop. Cleared when a new
    /// run starts.</summary>
    internal static class RunLog
    {
        public struct Entry { public int Cycle; public string Text; }

        private static readonly List<Entry> _entries = new List<Entry>();
        private const int Cap = 20000;

        public static IReadOnlyList<Entry> Entries => _entries;

        public static void Clear() => _entries.Clear();

        public static void Add(int cycle, string text)
        {
            if (_entries.Count >= Cap) _entries.RemoveAt(0);
            _entries.Add(new Entry { Cycle = cycle, Text = text });
        }
    }
}
