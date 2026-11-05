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
    /// The puzzle completed (CompletionScreen over the editor, titled with the puzzle's name), as the
    /// game draws it:
    ///  1. its message ("Your machine has successfully created the desired products.");
    ///  2. the scores: in a tutorial or custom puzzle, or in hermit mode, the game's notice instead;
    ///     otherwise cost, cycles and area (instructions in a production puzzle), each with the
    ///     figures the game prints under its histogram (now, best, previous; the top percentile when
    ///     global records are shown). The histogram curves themselves are not read yet;
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

            // One stop: Up / Down walk the message, the scores and the buttons.
            b.BeginStop("completion");
            b.AddItem(ControlId.Structural("completion:message"), Nodes.Text(Message));

            string notice = Notice(puzzle);
            if (notice != null)
                b.AddItem(ControlId.Structural("completion:notice"), Nodes.Text(() => notice));
            else
            {
                var third = puzzle.productionInfo.HasValue() ? ScoreMetric.Instructions : ScoreMetric.Footprint;
                foreach (var metric in new[] { ScoreMetric.Cost, ScoreMetric.Cycles, third })
                {
                    var m = metric;
                    b.AddItem(ControlId.Structural("completion:score:" + m), new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        Announcements = new List<NodeAnnouncement>
                        {
                            Nodes.LabelPart(() => MetricName(m)),
                            Nodes.ValuePart(() => Figures(m)),
                        },
                        SearchText = () => MetricName(m),
                    });
                }
            }

            bool tutorial = puzzle.isTutorial;
            foreach (var english in new[] { "Continue Editing", "Record GIF", "Return to Menu" })
            {
                string label = english;
                b.AddItem(ControlId.Structural("completion:" + label), Nodes.Button(() => GameText.Translate(label), () => Press(label),
                    enabled: () => label == "Return to Menu" || !tutorial));
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

        private static string MetricName(ScoreMetric m)
        {
            switch (m)
            {
                case ScoreMetric.Cost: return GameText.Translate("Cost");
                case ScoreMetric.Cycles: return GameText.Translate("Cycles");
                case ScoreMetric.Footprint: return GameText.Translate("Area");
                default: return GameText.Translate("Instructions");
            }
        }

        private static string Value(ScoreMetric m, int v) =>
            m == ScoreMetric.Cost ? GameText.Cost(v) : GameText.Clean(UIUtils.FormatMetricScore(m, v));

        // What the game prints under a histogram, with its own labels: "Now: 90g, Best: 80g, Prev: 100g",
        // then the top percentile when it shows global records.
        private string Figures(ScoreMetric m)
        {
            var g = Game;
            if (g == null || g.scoreData == null || !g.scoreData.TryGetValue(m, out var data) || data == null) return "";
            var parts = new List<string>();
            if (data.current.HasValue()) parts.Add(Labelled("Now: {0}", Value(m, data.current.GetValue())));
            if (data.best.HasValue()) parts.Add(Labelled("Best: {0}", Value(m, data.best.GetValue())));
            if (data.previous.HasValue()) parts.Add(Labelled("Prev: {0}", Value(m, data.previous.GetValue())));
            try
            {
                if (GameLogic.instance.settingsData.showGlobalRecords.Get())
                {
                    var top = GameLogic.instance.scoreManager.method_1376(data.saveKey);
                    if (top.HasValue()) parts.Add(GameText.Translate("Top Percentile") + " " + Value(m, top.GetValue()));
                }
            }
            catch (System.Exception e) { CoreLog.Warning("[completion] top percentile: " + e.Message); }
            return string.Join(", ", parts);
        }

        private static string Labelled(string format, string value)
        {
            try { return string.Format(GameText.Translate(format), value); }
            catch (System.FormatException e) { CoreLog.Warning("[completion] format '" + format + "': " + e.Message); return value; }
        }

        private static void Press(string english)
        {
            if (!ButtonPress.Press(Translations.Translate(english).GetTranslated()))
                CoreLog.Warning("[completion] the game did not draw a '" + english + "' button last frame.");
        }
    }
}
