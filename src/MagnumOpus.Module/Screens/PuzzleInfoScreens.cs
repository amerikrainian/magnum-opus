using System.Collections.Generic;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// A puzzle's info screen (PuzzleInfoScreen, titled with the puzzle's name), before the editor:
    ///  1. the solutions: name and scores as the game prints them ("Not Yet Solved / 3 Parts", or
    ///     cost, cycles and area or instructions); focusing one selects it (the game's buttons act on
    ///     the selection), Enter opens it in the editor;
    ///  2. New, Open, Rename, Copy, Delete: the game's own buttons, pressed through ButtonPress, so
    ///     Rename and Delete open the game's own message boxes (modeled separately);
    ///  3. the leaderboard area: the game's notice when there is nothing to show (tutorial, custom,
    ///     hermit mode, unsolved);
    ///  4. the story panel.
    /// The game's own keys keep working where they are not ours: Delete asks to delete, F2 renames.
    /// Escape passes to the game, which closes the screen.
    /// </summary>
    public sealed class PuzzleInfoGameScreen : GameScreen<PuzzleInfoScreen>
    {
        public override string Key => "game.puzzleinfo";

        public override string ScreenName => Game == null ? null : GameText.Of(Game.puzzle.puzzleName);

        private Solution[] Solutions() => Game == null ? new Solution[0] : SolutionManager.method_2144(Game.puzzle);

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g == null) return;
            var solutions = Solutions();

            b.BeginStop("solutions");
            b.PushContext(Strings.InfoSolutions, Strings.RoleList);
            for (int i = 0; i < solutions.Length; i++)
            {
                var solution = solutions[i];
                b.AddItem(ControlId.Structural("solution:" + i), new NodeVtable
                {
                    ControlType = ControlTypes.Item,
                    Announcements = new List<NodeAnnouncement>
                    {
                        Nodes.LabelPart(() => GameText.Clean(solution.name)),
                        Nodes.ValuePart(() => Scores(solution)),
                    },
                    SearchText = () => GameText.Clean(solution.name),
                    OnFocus = () => { var gg = Game; if (gg != null && !IsSelected(gg, solution)) gg.SelectSolution(solution); },
                    OnActivate = () => Press("Open"),
                });
            }
            b.PopContext();

            b.BeginStop("actions");
            foreach (var label in new[] { "New", "Open", "Rename", "Copy", "Delete" })
            {
                string english = label;
                b.AddItem(ControlId.Structural("info:" + english.ToLowerInvariant()), Nodes.Button(() => GameText.Translate(english),
                    () => Press(english), enabled: () => english == "New" || (Game != null && Game.selectedSolution.HasValue())));
            }

            b.BeginStop("leaderboard");
            b.AddItem(ControlId.Structural("info:leaderboard"), Nodes.Text(LeaderboardNotice));

            b.BeginStop("story");
            StoryPanelNodes.Build(b, "info:story", () => Game?.storyPanel);
        }

        private static bool IsSelected(PuzzleInfoScreen g, Solution s) => g.selectedSolution.HasValue() && g.selectedSolution.GetValue() == s;

        private static void Press(string english)
        {
            if (!ButtonPress.Press(Translations.Translate(english).GetTranslated()))
                CoreLog.Warning("[puzzle info] the game did not draw a '" + english + "' button last frame.");
        }

        // The solution row's second line, as the game formats it.
        private string Scores(Solution solution)
        {
            var g = Game;
            if (g == null) return "";
            var key = g.puzzle.productionInfo.HasValue() ? ScoreMetric.Instructions : ScoreMetric.Footprint;
            if (solution.scores.ContainsKey(ScoreMetric.Cost) && solution.scores.ContainsKey(ScoreMetric.Cycles) && solution.scores.ContainsKey(key))
            {
                string third = GameText.Translate(g.puzzle.productionInfo.HasValue() ? "Instrs" : "Area");
                return GameText.Cost(solution.scores[ScoreMetric.Cost]) + " " + GameText.Translate("Cost") + ", "
                    + solution.scores[ScoreMetric.Cycles] + " " + GameText.Translate("Cycles") + ", "
                    + solution.scores[key] + " " + third;
            }
            return GameText.Clean(string.Format(Translations.Translate("Not Yet Solved  /  {0} Parts").GetTranslated(), solution.parts.Count)).Replace("  /  ", ", ");
        }

        // What the game draws where the histograms go when it has none to show.
        private string LeaderboardNotice()
        {
            var g = Game;
            if (g == null) return "";
            if (g.puzzle.isTutorial) return GameText.Translate("Histograms and leaderboards are not available for tutorial puzzles.");
            if (g.puzzle.isCustom) return GameText.Translate("Histograms and leaderboards are not available for custom puzzles.");
            if (GameLogic.instance.settingsData.enableHermitMode.Get()) return GameText.Translate("Histograms and leaderboards are hidden when *hermit mode* is enabled.");
            if (!GameLogic.instance.saveData.IsComplete(g.puzzle)) return GameText.Translate("Solve this puzzle to see its histograms and leaderboards.");
            return GameText.Translate("Histograms");
        }
    }
}
