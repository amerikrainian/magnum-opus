using System.Collections.Generic;
using System.Linq;

namespace MagnumOpus.Module.Game
{
    /// <summary>
    /// The arms a simulation error involves ("arm 1, arm 2, Atoms may not collide."). The game's
    /// error keeps only its message and the places it marks, so each place is matched back to what
    /// the sim put there, read live from the stopped cycle:
    /// - at time 0 (an instruction the arm cannot run, two holders pulling one molecule apart) the
    ///   places are cycle-start hexes: arm bases for the track and piston errors, grippers for the
    ///   grab and pulling ones (a gripper may rest over another arm's base, so the message decides);
    /// - later in the cycle (collisions, leaving a chamber) the places are collider centres, rebuilt
    ///   here exactly as Sim.SimulateCycleMovements builds them at the error's time: atoms of each
    ///   molecule (owned by every arm gripping it; a free one by none), atoms fixed to a part (a
    ///   wheel's), arm bases, grippers. Atoms a glyph emerges own no arm and are left out.
    /// A place nothing matches adds no arm, so the message reads as the game's alone.
    /// </summary>
    public static class ErrorArms
    {
        // The places are the sim's own floats computed by the same calls: a hair of slack.
        private const float Slack = 0.5f;

        private sealed class Point
        {
            public Vector2 Pos;
            public List<Part> Arms;
        }

        /// <summary>The involved arms' tape numbers, ascending; empty when none can be told.</summary>
        public static List<int> Of(SolutionEditorScreen ed, SimError e)
        {
            var numbers = new List<int>();
            if (ed == null || e?.locations == null || e.locations.Length == 0 || !Board.Running(ed)) return numbers;
            var points = e.errorTime <= 0f ? AtCycleStart(ed, e.errorMessage) : Colliders(ed, e.errorTime);
            foreach (var place in e.locations)
                foreach (var p in points)
                {
                    if (Vector2.Distance(p.Pos, place) >= Slack) continue;
                    foreach (var arm in p.Arms)
                    {
                        int n = Board.ArmNumber(ed, arm);
                        if (n > 0 && !numbers.Contains(n)) numbers.Add(n);
                    }
                }
            numbers.Sort();
            return numbers;
        }

        // Sim.RunCycleInstructions reports these at the arm's base, the rest at a gripper.
        private static bool AtBase(string message) =>
            message == (string)Sim.simErrorNoTrack || message == (string)Sim.simErrorLeaveTrack
            || message == (string)Sim.simErrorNotPiston || message == (string)Sim.simErrorRetract
            || message == (string)Sim.simErrorExtend;

        private static bool AtGripper(string message) =>
            message == (string)Sim.simErrorGrab || message == (string)Sim.simErrorMultDir
            || message == (string)Sim.simErrorBoundMDir;

        private static List<Point> AtCycleStart(SolutionEditorScreen ed, string message)
        {
            var sim = ed.GetSimulation();
            bool bases = AtBase(message), grippers = AtGripper(message);
            if (!bases && !grippers) bases = grippers = true; // a message this table does not know
            var points = new List<Point>();
            foreach (var arm in Board.Arms(ed))
            {
                var owner = new List<Part> { arm };
                if (bases) points.Add(new Point { Pos = HexGrid.standardGrid.ToPixelCoords(sim.GetSimState(arm).oldPosition), Arms = owner });
                if (grippers && arm.subparts != null)
                    foreach (var grip in arm.subparts)
                        points.Add(new Point { Pos = HexGrid.standardGrid.ToPixelCoords(sim.GetSimState(grip).oldPosition), Arms = owner });
            }
            return points;
        }

        private static List<Point> Colliders(SolutionEditorScreen ed, float time)
        {
            var sim = ed.GetSimulation();
            var arms = Board.Arms(ed);
            var points = new List<Point>();
            foreach (var molecule in sim.GetMolecules())
            {
                var owners = arms.Where(a => a.subparts != null && a.subparts.Any(g =>
                {
                    var held = sim.GetSimState(g).heldMolecule;
                    return held.HasValue() && ReferenceEquals(held.GetValue(), molecule);
                })).ToList();
                if (owners.Count == 0) continue;
                Vector2 origin = Vector2.Zero;
                HexIndex originHex = new HexIndex(0, 0);
                float turn = 0f;
                if (ed.GetHolder(molecule).GetOrDefault(out var holder))
                {
                    var state = ed.GetIntermState(holder, Vector2.Zero, time);
                    origin = state.pos;
                    originHex = sim.GetSimState(holder).newPosition;
                    turn = state.leftoverDeltaPivot;
                }
                foreach (var atom in molecule.GetAtoms())
                    points.Add(new Point { Pos = origin + HexGrid.standardGrid.ToPixelCoords(atom.Key - originHex).Rotated(turn), Arms = owners });
            }
            foreach (var part in ed.GetSolution().CollectPartsAndSubparts().Where(p => p.HasAtoms()))
            {
                var root = part.GetRootPart();
                if (!arms.Contains(root)) continue;
                var owner = new List<Part> { root };
                var state = ed.GetIntermState(part, Vector2.Zero, time);
                foreach (var offset in part.GetType().attachedAtoms.Keys)
                    points.Add(new Point { Pos = state.pos + HexGrid.standardGrid.ToPixelCoords(offset).Rotated(state.rotation), Arms = owner });
            }
            foreach (var arm in arms)
            {
                var owner = new List<Part> { arm };
                points.Add(new Point { Pos = ed.GetIntermState(arm, Vector2.Zero, time).pos, Arms = owner });
                if (arm.subparts != null)
                    foreach (var grip in arm.subparts)
                        points.Add(new Point { Pos = ed.GetIntermState(grip, Vector2.Zero, time).pos, Arms = owner });
            }
            return points;
        }
    }
}
