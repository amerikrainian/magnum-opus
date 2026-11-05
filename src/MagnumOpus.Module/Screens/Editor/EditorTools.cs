using MagnumOpus.Module.Game;

namespace MagnumOpus.Module.Screens.Editor
{
    /// <summary>
    /// Two of the solution editor's toolbar buttons (SolutionEditorProgramPanel's icon buttons, a
    /// private builder ButtonPress cannot reach): their names, enabled rules and click code, as the
    /// game has them. Show Information reopens the puzzle's tip; Show Diagram opens the alchemical
    /// reference diagram (<see cref="PeriodicTableGameScreen"/>) and marks it learned, which stops
    /// the button blinking (it blinks on Refined Gold until then).
    /// </summary>
    internal static class EditorTools
    {
        private static bool Stopped(SolutionEditorScreen ed) => ed != null && ed.GetSimPlayState() == SimPlayState.Stopped;

        public static string InformationLabel => GameText.Translate("Show Information");
        public static string InformationDescription => GameText.Translate("Displays the information shown when first starting this puzzle.");
        public static string DiagramLabel => GameText.Translate("Show Diagram");
        public static string DiagramDescription => GameText.Translate("Displays an alchemical reference diagram.");

        public static bool InformationEnabled(SolutionEditorScreen ed) =>
            Stopped(ed) && ed.GetSolution() != null && ed.GetSolution().GetPuzzle().puzzleTip.HasValue();

        public static bool DiagramEnabled(SolutionEditorScreen ed) => Stopped(ed);

        public static void ShowInformation(SolutionEditorScreen ed)
        {
            if (!InformationEnabled(ed)) return;
            GameLogic.instance.PushScreen(new PuzzleTipScreen(ed.GetSolution().GetPuzzle().puzzleTip.GetValue()));
            Assets.sounds.ui_modal.PlaySound(1f);
        }

        public static void ShowDiagram(SolutionEditorScreen ed)
        {
            if (!DiagramEnabled(ed)) return;
            GameLogic.instance.saveData.SetDiagramLearned(true);
            GameLogic.instance.PushScreen(new PeriodicTableScreen(true), MaybeHelper.empty, Transitions.raise);
        }
    }
}
