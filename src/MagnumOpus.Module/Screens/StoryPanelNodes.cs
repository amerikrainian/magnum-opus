using System;
using System.Collections.Generic;
using MagnumOpus.Core;
using MagnumOpus.Module.Game;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The story panel (the game's StoryPanel, on the options and puzzle info screens) as lines, the
    /// way it draws them (both screens speak them on opening, no stop): without a vignette, the periodic table quote and its author; with a
    /// single-event vignette, its three texts; otherwise the vignette's dialogue lines the player has
    /// reached so far (the save's vignette progress), each "Speaker: line".
    /// </summary>
    public static class StoryPanelNodes
    {
        public static List<string> Lines(StoryPanel panel)
        {
            var lines = new List<string>();
            if (panel == null) return lines;
            try
            {
                if (!panel.vignette.HasValue())
                {
                    lines.Add(GameText.Clean(PeriodicTableScreen.quote));
                    lines.Add(GameText.Clean(PeriodicTableScreen.name));
                    return lines;
                }
                var localized = panel.vignette.GetValue();
                var vignette = localized.GetVignette();
                int total = 0;
                foreach (var section in vignette.events) total += section.Count;
                if (total == 1)
                {
                    foreach (var text in panel.parsedText)
                        if (!string.IsNullOrWhiteSpace(text)) lines.Add(GameText.Clean(text));
                    return lines;
                }
                int progress = PuzzleSelectScreen.unlockAllMode ? int.MaxValue : GameLogic.instance.saveData.GetVignetteProgress(localized);
                int index = 0;
                foreach (var section in vignette.events)
                {
                    foreach (var e in section)
                    {
                        if (index >= progress) return lines;
                        index++;
                        if (!e.IsLineEvent()) continue;
                        var line = e.ToLineEvent();
                        lines.Add(GameText.Of(line.portrait.name) + ": " + GameText.Clean(line.line));
                    }
                }
            }
            catch (Exception ex)
            {
                CoreLog.Warning("[story] reading the story panel failed: " + ex.Message);
            }
            return lines;
        }
    }
}
