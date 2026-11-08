using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.Tutorial;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.Screens.Editor;

namespace MagnumOpus.Module.Tutorial
{
    /// <summary>
    /// The lessons, by puzzle id. A step's words may use only the game's own notes and highlights and
    /// the names of keys: never a rotation, a length or an order of instructions (user rule
    /// 2026-11-08: no more of the solution than the game's tutorial gives). A condition may confirm a
    /// goal a note states. Text is "tut.&lt;lesson&gt;.&lt;step&gt;" (+ ".after") in the strings table.
    /// </summary>
    internal static class TutorialScripts
    {
        private const string Introduction = "P044", Arms = "P001", Pivots = "P004", Pistons = "P002", Tracks = "P003";

        public static bool Has(string puzzleId) =>
            puzzleId == Introduction || puzzleId == Arms || puzzleId == Pivots || puzzleId == Pistons || puzzleId == Tracks;

        public static List<TutorialStep> For(string puzzleId, TutorialContext c)
        {
            switch (puzzleId)
            {
                case Introduction: return IntroductionSteps(c);
                case Arms: return ArmsSteps(c);
                case Pivots: return PivotsSteps(c);
                case Pistons: return PistonsSteps(c);
                case Tracks: return TracksSteps(c);
                default: return new List<TutorialStep>();
            }
        }

        private static string K(string action) => Tutorials.Key(action);
        private static string Instr(string name) => K(EditorActions.InstructionPrefix + name);

        private static readonly string[] HexMoves =
        {
            EditorActions.HexNW, EditorActions.HexNE, EditorActions.HexW, EditorActions.HexE, EditorActions.HexSW, EditorActions.HexSE,
        };

        // ---- Lesson: Introduction: the machine is finished and cannot be edited; reading and running it. ----

        private static List<TutorialStep> IntroductionSteps(TutorialContext c)
        {
            const string P = "tut.intro.";
            return new List<TutorialStep>
            {
                Latch(P, "move", () => c.Pressed(HexMoves), () => new object[] { K(EditorActions.TutorialRepeat), K("mod.help"), K(HexMoves[0]), K(HexMoves[1]), K(HexMoves[2]), K(HexMoves[3]), K(HexMoves[4]), K(HexMoves[5]) }),
                Latch(P, "neighbours", () => c.Pressed(EditorActions.Neighbours), () => new object[] { K(EditorActions.Neighbours) }),
                Latch(P, "reagent", () => c.Pressed(EditorActions.ItemNext, EditorActions.ItemPrev) && c.CursorOn(p => p.GetType().isInput),
                    () => new object[] { c.Note(0, 0) + " " + c.Note(0, 1), K(EditorActions.CatPrev), K(EditorActions.CatNext), K(EditorActions.ItemPrev), K(EditorActions.ItemNext) }),
                Latch(P, "molecule", () => c.Pressed(EditorActions.Molecule), () => new object[] { K(EditorActions.Molecule) }),
                Latch(P, "product", () => c.Pressed(EditorActions.ItemNext, EditorActions.ItemPrev) && c.CursorOn(p => p.GetType().IsOutput()),
                    () => new object[] { c.Note(1, 0) + " " + c.Note(1, 1) }),
                Latch(P, "arm", () => c.Pressed(EditorActions.ArmPrefix + 1), () => new object[] { K(EditorActions.ArmPrefix + 1) }),
                Latch(P, "tape", () => Equals(c.Stop, EditorContext.StopProgram), () => new object[] { c.Note(2, 0) + " " + c.Note(2, 1), K(EditorActions.Jump) }),
                Latch(P, "cells", () => c.Pressed(UiActions.Left, UiActions.Right), () => new object[] { K(UiActions.Left), K(UiActions.Right), K(UiActions.Up), K(UiActions.Down) }),
                Latch(P, "step", () => c.Pressed(EditorActions.Step) && c.Cycle >= 3 && c.RunState != SimPlayState.Stopped, () => new object[] { K(EditorActions.Step) }),
                Latch(P, "stop", () => c.RunState == SimPlayState.Stopped, () => new object[] { K(UiActions.Back) }),
                Latch(P, "play", () => c.RunState == SimPlayState.Playing, () => new object[] { c.Note(3, 0) + " " + c.Note(3, 1), K(EditorActions.Play) }),
                Latch(P, "fast", () => c.Pressed(EditorActions.Fast), () => new object[] { K(EditorActions.Fast) }),
            };
        }

        // ---- Lesson: Arms: the player's first part and program. The game highlights the base hex
        // and its notes say to put the gripper over the reagent and to pick it up and move it. ----

        private static List<TutorialStep> ArmsSteps(TutorialContext c)
        {
            const string P = "tut.arms.";
            Func<bool> armPlaced = () => c.PlayerArms.Any();
            Func<bool> anyInstruction = () => c.PlayerArms.Any(a => a.program.Count() > 0);
            // Where the arms stand as built: a running arm swings away from the reagent.
            Func<bool> gripperOverReagent = c.WhileStopped(() =>
            {
                var free = new HashSet<HexIndex>(c.FreeReagents);
                return c.PlayerArms.Any(a => Board.Grippers(c.Ed, a).Any(g => free.Contains(g.Key)));
            });
            Func<string> reagentHex = c.WhileStopped(() => string.Join(", ", c.FreeReagents.Select(Hex.Coords)));
            return new List<TutorialStep>
            {
                Latch(P, "goal", () => armPlaced() || c.FreeHighlights.Contains(c.Cursor),
                    () => new object[] { c.Note(0, 0), K(EditorActions.CatPrev), K(EditorActions.CatNext), K(EditorActions.ItemNext) }),
                Latch(P, "carry", () => armPlaced() || c.Editor.Carry.Active, () => new object[] { K(UiActions.Activate) }),
                Live(P, "place", armPlaced, () => new object[] { K(EditorActions.RotateCcw), K(EditorActions.RotateCw), K(EditorActions.Longer), K(EditorActions.Shorter), K(UiActions.Activate), K(UiActions.Back) }),
                Live(P, "reach", gripperOverReagent, () => new object[] { reagentHex(), K(EditorActions.RotateCcw), K(EditorActions.RotateCw), K(UiActions.Activate) }, after: true),
                Latch(P, "tape", () => anyInstruction() || (Equals(c.Stop, EditorContext.StopProgram) && c.PlayerArms.Contains(ProgramSection.RowArm(c.Editor))),
                    () => new object[] { K(EditorActions.Jump), K(UiActions.Up), K(UiActions.Down) }),
                Live(P, "grab", () => c.PlayerArms.Any(a => a.program.HasInstruction(InstructionTypes.grab)), () => new object[] { c.Note(0, 1), Instr("grab") }),
                Latch(P, "board", () => c.Pressed(TutorialEvents.ProgrammedFromBoard), () => new object[] { K(EditorActions.Jump) }),
                Latch(P, "edit", () => c.Pressed(EditorActions.Delete, UiActions.Secondary, EditorActions.Undo), () => new object[] { K(EditorActions.Delete), K(UiActions.Secondary), K(EditorActions.Undo) }),
                Latch(P, "list", () => Equals(c.Stop, InstructionTraySection.Stop), () => new object[] { K(UiActions.Next), K(EditorActions.Details) }),
                Latch(P, "finish", () => c.RunState != SimPlayState.Stopped, () => new object[] { K("mod.help") }),
            };
        }

        // ---- Lesson: Pivots: the game's own arm shows a pivot, then the player places an arm on the
        // highlight. The game's notes say only to place an arm and to move the reagent. ----

        private static List<TutorialStep> PivotsSteps(TutorialContext c)
        {
            const string P = "tut.pivots.";
            Func<bool> armPlaced = () => c.PlayerArms.Any();
            return new List<TutorialStep>
            {
                Latch(P, "meet", () => c.Pressed(EditorActions.ArmPrefix + 1), () => new object[] { K(EditorActions.ArmPrefix + 1) }),
                Latch(P, "tape", () => Equals(c.Stop, EditorContext.StopProgram) && c.Pressed(UiActions.Left, UiActions.Right), () => new object[] { K(EditorActions.Jump), K(UiActions.Left), K(UiActions.Right) }),
                Latch(P, "describe", () => Equals(c.Stop, InstructionTraySection.Stop) && c.Pressed(EditorActions.Details), () => new object[] { K(UiActions.Next), K(EditorActions.Details) }),
                Latch(P, "watch", () => c.Pressed(EditorActions.Step) && c.Cycle >= 3 && c.RunState != SimPlayState.Stopped, () => new object[] { K(EditorActions.Step) }),
                Latch(P, "held", () => Board.Arms(c.Ed).Where(a => a.GetIsFixed()).Any(a => Board.Grippers(c.Ed, a).Any(g => g.Key == c.Cursor)), () => new object[0]),
                Latch(P, "stop", () => c.RunState == SimPlayState.Stopped, () => new object[] { K(UiActions.Back) }),
                Latch(P, "goal", () => armPlaced() || c.FreeHighlights.Contains(c.Cursor), () => new object[] { c.Note(0, 0), K(EditorActions.CatPrev), K(EditorActions.CatNext) }),
                Live(P, "carry", armPlaced, () => new object[] { K(UiActions.Activate) }),
                Latch(P, "reagent", () => c.Pressed(EditorActions.Molecule) && c.CursorOn(p => p.GetType().isInput), () => new object[] { K(EditorActions.Molecule) }),
                Live(P, "program", () => c.PlayerArms.Any(a => a.program.Count() > 0), () => new object[0]),
            };
        }

        // ---- Lesson: Pistons: the game's arm shows a reset, then the player's own piston. ----

        private static List<TutorialStep> PistonsSteps(TutorialContext c)
        {
            const string P = "tut.pistons.";
            Func<bool> armPlaced = () => c.PlayerArms.Any();
            return new List<TutorialStep>
            {
                Latch(P, "meet", () => c.Pressed(EditorActions.ArmPrefix + 1), () => new object[0]),
                Latch(P, "tape", () => Equals(c.Stop, EditorContext.StopProgram) && c.Pressed(UiActions.Home, UiActions.End), () => new object[] { K(EditorActions.Jump), K(UiActions.Home), K(UiActions.End) }),
                Latch(P, "reset", () => c.Pressed(UiActions.Left, UiActions.Right), () => new object[] { c.Note(1, 0) + " " + c.Note(1, 1), K(UiActions.Left), K(UiActions.Right) }),
                Latch(P, "goal", () => armPlaced() || c.FreeHighlights.Contains(c.Cursor), () => new object[] { c.Note(0, 0), K(EditorActions.CatPrev), K(EditorActions.CatNext) }),
                Live(P, "carry", armPlaced, () => new object[] { K(UiActions.Activate) }),
                Live(P, "program", () => c.PlayerArms.Any(a => a.program.Count() >= 2), () => new object[0]),
                Latch(P, "insert", () => c.Pressed(TutorialEvents.Inserted), () => new object[0]),
                Latch(P, "shift", () => c.Pressed(EditorActions.ShiftLeft, EditorActions.ShiftRight), () => new object[] { K(EditorActions.ShiftLeft), K(EditorActions.ShiftRight) }),
                Latch(P, "done", () => c.RunState != SimPlayState.Stopped, () => new object[0]),
            };
        }

        // ---- Lesson: Tracks: the game highlights the hexes the track covers; its notes say to make
        // a track between the reagent and the product and to put an arm on it. ----

        private static List<TutorialStep> TracksSteps(TutorialContext c)
        {
            const string P = "tut.tracks.";
            Func<Part> track = () => c.Solution.parts.FirstOrDefault(p => p.GetType().isTrack && !p.GetIsFixed());
            Func<bool> trackPlaced = () => track() != null;
            Func<bool> trackDrawn = () => track() != null && !c.Editor.Draw.Active && track().GetTrack().Count >= 2;
            return new List<TutorialStep>
            {
                Latch(P, "meet", () => c.Pressed(EditorActions.ArmPrefix + 1), () => new object[] { K(EditorActions.ArmPrefix + 1) }),
                Latch(P, "read", () => c.Pressed(HexMoves) && c.CursorOn(p => p.GetType().isTrack), () => new object[0]),
                Latch(P, "parts", () => c.Pressed(EditorActions.PartPrev, EditorActions.PartNext), () => new object[] { K(EditorActions.ArmPrefix + 1), K(EditorActions.PartPrev), K(EditorActions.PartNext) }),
                Latch(P, "goal", () => trackPlaced() || c.FreeHighlights.Contains(c.Cursor), () => new object[0]),
                Latch(P, "carry", () => trackPlaced() || c.Editor.Carry.Active, () => new object[] { K(UiActions.Activate) }),
                Latch(P, "draw", trackDrawn, () => new object[] { K(UiActions.Activate) }),
                Latch(P, "extend", () => c.Pressed(EditorActions.DrawTrack) && !c.Editor.Draw.Active, () => new object[] { K(EditorActions.DrawTrack), K(UiActions.Activate) }),
                Live(P, "arm", () => c.PlayerArms.Any(), () => new object[] { c.Note(0, 1) }),
                Latch(P, "done", () => c.RunState != SimPlayState.Stopped, () => new object[] { Instr("move_plus"), Instr("move_minus") }),
            };
        }

        // ---- builders ----

        private static TutorialStep Latch(string prefix, string id, Func<bool> met, Func<object[]> args, bool after = false) =>
            Step(prefix, id, met, args, latches: true, after);

        private static TutorialStep Live(string prefix, string id, Func<bool> met, Func<object[]> args, bool after = false) =>
            Step(prefix, id, met, args, latches: false, after);

        private static TutorialStep Step(string prefix, string id, Func<bool> met, Func<object[]> args, bool latches, bool after) =>
            new TutorialStep
            {
                Id = id,
                Latches = latches,
                Met = met,
                Text = () => Strings.F(prefix + id, args()),
                After = after ? (Func<string>)(() => Strings.Get(prefix + id + ".after")) : null,
            };
    }

    /// <summary>Things that happened with no action of their own to report them.</summary>
    internal static class TutorialEvents
    {
        public const string ProgrammedFromBoard = "tutorial.programmed_from_board";
        public const string Inserted = "tutorial.inserted";
    }
}
