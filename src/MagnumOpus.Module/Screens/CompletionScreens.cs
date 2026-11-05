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
                // A table, as SpeechChem's: a column per metric (its name spoken on crossing), the
                // figures first, then one row per drawn histogram bar; Up / Down walk a column,
                // Left / Right keep the row.
                var third = puzzle.productionInfo.HasValue() ? ScoreMetric.Instructions : ScoreMetric.Footprint;
                foreach (var metric in new[] { ScoreMetric.Cost, ScoreMetric.Cycles, third })
                {
                    var m = metric;
                    var rows = HistogramRows(m);
                    b.PushContext(MetricName(m), null, positions: false);
                    b.StartColumn();
                    var top = ControlId.Structural("completion:score:" + m);
                    b.AddItem(top, Nodes.Text(() => Figures(m)));
                    var last = top;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        string row = rows[i];
                        last = ControlId.Structural("completion:bar:" + m + ":" + i);
                        b.AddItem(last, Nodes.Text(() => row));
                    }
                    b.EndColumn();
                    b.PopContext();
                    tops.Add(top);
                    bottoms.Add(last);
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

        // The histogram's bars as rows, read the way SpeechChem reads its charts: each drawn bar's
        // score range and its share of the players ("60g to 72g: 31%"), the bar under the game's
        // marker tagged "now". Nothing when the game has no data (it then draws an empty frame).
        private List<string> HistogramRows(ScoreMetric m)
        {
            var rows = new List<string>();
            var g = Game;
            if (g == null || g.scoreData == null || !g.scoreData.TryGetValue(m, out var data) || data == null) return rows;
            if (!data.histogramData.HasValue()) return rows;
            var h = data.histogramData.GetValue();
            int? now = data.current.HasValue() ? data.current.GetValue() : (int?)null;
            foreach (var bin in HistogramBins.Bins(h.buckets, h.maxScore, now))
            {
                string pct = bin.Percent == 0 && bin.Drawn ? Strings.HistogramTiny : Strings.HistogramPercent(bin.Percent);
                string tag = bin.Marked ? ", " + Strings.HistogramNow : "";
                rows.Add(bin.Low == bin.High
                    ? Strings.HistogramBinOne(Value(m, bin.Low), pct + tag)
                    : Strings.HistogramBin(Value(m, bin.Low), Value(m, bin.High), pct + tag));
            }
            return rows;
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
