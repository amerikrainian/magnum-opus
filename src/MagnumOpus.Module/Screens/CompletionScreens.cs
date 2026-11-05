using System.Collections.Generic;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.Text;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The puzzle completed (CompletionScreen over the editor, titled with the puzzle's name), as the
    /// game draws it:
    ///  1. its message ("Your machine has successfully created the desired products.");
    ///  2. the scores: in a tutorial or custom puzzle, or in hermit mode, the game's notice instead;
    ///     otherwise a table of cost, cycles and area (instructions in a production puzzle): each
    ///     column the figures the game prints under its histogram (now, best, previous; the top
    ///     percentile when global records are shown), then its bars ("60g to 72g: 31%", the share of
    ///     players; the marker's bar tagged "now"; HistogramBins);
    ///  3. Continue Editing, Record GIF (both disabled in a tutorial, as the game has them) and Return
    ///     to Menu, the game's own buttons through ButtonPress.
    /// All in one stop, in that order. Escape passes to the game: outside a tutorial it continues
    /// editing (as do Tab and Space there).
    /// </summary>
    public sealed class CompletionGameScreen : GameScreen<CompletionScreen>
    {
        public override string Key => "game.completion";

        private Puzzle Puzzle => Game?.solEditScrn?.GetSolution()?.GetPuzzle();

        public override string ScreenName => Puzzle == null ? null : GameText.Of(Puzzle.puzzleName);

        public override void Build(GraphBuilder b)
        {
            var puzzle = Puzzle;
            if (puzzle == null) return;

            // One stop: the message, then the scores (the notice, or a table of the three metrics),
            // then the buttons.
            b.BeginStop("completion");
            var message = ControlId.Structural("completion:message");
            b.AddItem(message, Nodes.Text(Message));

            var tops = new List<ControlId>();
            var bottoms = new List<ControlId>();
            string notice = Notice(puzzle);
            if (notice != null)
                b.AddItem(ControlId.Structural("completion:notice"), Nodes.Text(() => notice));
            else
            {
                foreach (var c in ScoreTable.Build(b, "completion", () => Game?.scoreData, ScoreTable.Metrics(puzzle), comparing: false))
                {
                    tops.Add(c.Top);
                    bottoms.Add(c.Bottom);
                }
            }

            bool tutorial = puzzle.isTutorial;
            ControlId firstButton = null;
            foreach (var english in new[] { "Continue Editing", "Record GIF", "Return to Menu" })
            {
                string label = english;
                var id = ControlId.Structural("completion:" + label);
                firstButton = firstButton ?? id;
                b.AddItem(id, Nodes.Button(() => GameText.Translate(label), () => Press(label),
                    enabled: () => label == "Return to Menu" || !tutorial));
            }

            // The table's columns are raw: wire them to the message above and the buttons below.
            if (tops.Count > 0)
            {
                b.Connect(message, GraphDir.Down, tops[0]);
                foreach (var t in tops) b.Connect(t, GraphDir.Up, message);
                foreach (var bottom in bottoms) b.Connect(bottom, GraphDir.Down, firstButton);
                b.Connect(firstButton, GraphDir.Up, bottoms[0]);
            }
        }

        private string Message()
        {
            var puzzle = Puzzle;
            if (puzzle == null) return "";
            return GameText.Translate(puzzle.outputs.Length == 1
                ? "Your machine has successfully created the desired product."
                : "Your machine has successfully created the desired products.");
        }

        // The game's text where it draws no histograms; null where it draws them.
        private static string Notice(Puzzle puzzle)
        {
            if (puzzle.isTutorial) return GameText.Translate("Histograms and leaderboards are not available for tutorial puzzles.");
            if (puzzle.isCustom) return GameText.Translate("Histograms and leaderboards are not available for custom puzzles.");
            if (GameLogic.instance.settingsData.enableHermitMode.Get()) return GameText.Translate("Histograms and leaderboards are hidden when *hermit mode* is enabled.");
            return null;
        }

        private static void Press(string english)
        {
            if (!ButtonPress.Press(Translations.Translate(english).GetTranslated()))
                CoreLog.Warning("[completion] the game did not draw a '" + english + "' button last frame.");
        }
    }
}
