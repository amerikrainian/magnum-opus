using System;
using System.Collections.Generic;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.Text;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The game's three score histograms (UIUtils.RenderHistogram) as a table, SpeechChem's layout:
    /// a graph column per metric (cost, cycles, area or instructions; its name the column's context,
    /// spoken on crossing), first the figures the game prints under the chart, then one row per drawn
    /// bar, "51g to 63g: 55%, now" (the bar's share of the players, <see cref="HistogramBins"/>).
    /// Two modes, as the game draws them:
    ///  - a run just finished (the completion screen): "Now: 60g, Best: 50g, Prev: 70g", the bar
    ///    under the run's marker tagged "now";
    ///  - comparing (puzzle info, renderDiff): "This: 60g" (the selected solution, N/A when it has no
    ///    score) and "Best: 50g", the bars under those two markers tagged "this" and "best".
    /// Both add the top percentile when the game shows global records. Up / Down walk a column,
    /// Left / Right keep the row.
    /// </summary>
    internal static class ScoreTable
    {
        public struct Column { public ControlId Top, Bottom; }

        public static ScoreMetric[] Metrics(Puzzle puzzle) => new[]
        {
            ScoreMetric.Cost, ScoreMetric.Cycles,
            puzzle.productionInfo.HasValue() ? ScoreMetric.Instructions : ScoreMetric.Footprint,
        };

        /// <summary>Add the columns; their tops and bottoms are returned for wiring to what is above
        /// and below (column nodes are raw).</summary>
        public static List<Column> Build(GraphBuilder b, string idPrefix, Func<Dictionary<ScoreMetric, ScoreData>> scores,
            IEnumerable<ScoreMetric> metrics, bool comparing)
        {
            var columns = new List<Column>();
            foreach (var metric in metrics)
            {
                var m = metric;
                var rows = Rows(Data(scores, m), m, comparing);
                b.PushContext(MetricName(m), null, positions: false);
                b.StartColumn();
                var top = ControlId.Structural(idPrefix + ":score:" + m);
                b.AddItem(top, Nodes.Text(() => Figures(Data(scores, m), m, comparing)));
                var last = top;
                for (int i = 0; i < rows.Count; i++)
                {
                    string row = rows[i];
                    last = ControlId.Structural(idPrefix + ":bar:" + m + ":" + i);
                    b.AddItem(last, Nodes.Text(() => row));
                }
                b.EndColumn();
                b.PopContext();
                columns.Add(new Column { Top = top, Bottom = last });
            }
            return columns;
        }

        private static ScoreData Data(Func<Dictionary<ScoreMetric, ScoreData>> scores, ScoreMetric m)
        {
            var d = scores?.Invoke();
            return d != null && d.TryGetValue(m, out var data) ? data : null;
        }

        public static string MetricName(ScoreMetric m)
        {
            switch (m)
            {
                case ScoreMetric.Cost: return GameText.Translate("Cost");
                case ScoreMetric.Cycles: return GameText.Translate("Cycles");
                case ScoreMetric.Footprint: return GameText.Translate("Area");
                default: return GameText.Translate("Instructions");
            }
        }

        public static string Value(ScoreMetric m, int v) =>
            m == ScoreMetric.Cost ? GameText.Cost(v) : GameText.Clean(UIUtils.FormatMetricScore(m, v));

        // What the game prints under the chart, with its own labels, then the top percentile.
        private static string Figures(ScoreData data, ScoreMetric m, bool comparing)
        {
            if (data == null) return "";
            var parts = new List<string>();
            if (data.current.HasValue())
            {
                parts.Add(Labelled("Now: {0}", Value(m, data.current.GetValue())));
                if (data.best.HasValue()) parts.Add(Labelled("Best: {0}", Value(m, data.best.GetValue())));
                if (data.previous.HasValue()) parts.Add(Labelled("Prev: {0}", Value(m, data.previous.GetValue())));
            }
            else if (comparing)
            {
                parts.Add(Labelled("This: {0}", data.previous.HasValue() ? Value(m, data.previous.GetValue()) : GameText.Translate("N/A")));
                if (data.best.HasValue()) parts.Add(Labelled("Best: {0}", Value(m, data.best.GetValue())));
            }
            else if (data.best.HasValue()) parts.Add(Labelled("Best: {0}", Value(m, data.best.GetValue())));
            try
            {
                if (GameLogic.instance.settingsData.showGlobalRecords.Get())
                {
                    var top = GameLogic.instance.scoreManager.method_1376(data.saveKey);
                    if (top.HasValue()) parts.Add(GameText.Translate("Top Percentile") + " " + Value(m, top.GetValue()));
                }
            }
            catch (Exception e) { CoreLog.Warning("[scores] top percentile: " + e.Message); }
            return string.Join(", ", parts);
        }

        // The bars, the marked ones tagged as the game marks them (a run's line; else this and best).
        private static List<string> Rows(ScoreData data, ScoreMetric m, bool comparing)
        {
            var rows = new List<string>();
            if (data == null || !data.histogramData.HasValue()) return rows;
            var h = data.histogramData.GetValue();
            int? first = null, second = null;
            string firstTag = null, secondTag = null;
            if (data.current.HasValue()) { first = data.current.GetValue(); firstTag = Strings.HistogramNow; }
            else if (comparing)
            {
                if (data.previous.HasValue()) { first = data.previous.GetValue(); firstTag = Strings.HistogramThis; }
                if (data.best.HasValue()) { second = data.best.GetValue(); secondTag = Strings.HistogramBest; }
            }
            else if (data.best.HasValue()) { second = data.best.GetValue(); secondTag = Strings.HistogramBest; }
            foreach (var bin in HistogramBins.Bins(h.buckets, h.maxScore, first, second))
            {
                string text = bin.Percent == 0 && bin.Drawn ? Strings.HistogramTiny : Strings.HistogramPercent(bin.Percent);
                if (bin.Marked) text += ", " + firstTag;
                if (bin.Marked2) text += ", " + secondTag;
                rows.Add(bin.Low == bin.High
                    ? Strings.HistogramBinOne(Value(m, bin.Low), text)
                    : Strings.HistogramBin(Value(m, bin.Low), Value(m, bin.High), text));
            }
            return rows;
        }

        private static string Labelled(string format, string value)
        {
            try { return string.Format(GameText.Translate(format), value); }
            catch (FormatException e) { CoreLog.Warning("[scores] format '" + format + "': " + e.Message); return value; }
        }
    }
}
