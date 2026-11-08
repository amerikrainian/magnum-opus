using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>What the story panel shows now (<see cref="StoryPanelNodes.Read"/>).</summary>
    public sealed class StoryView
    {
        public readonly List<string> Items = new List<string>();

        /// <summary>The items are dialogue lines (not the quote or a journal page, each one piece).</summary>
        public bool Dialogue;

        /// <summary>The panel is waiting for a press to show the next line (the game's pulsing arrow).</summary>
        public bool More;

        /// <summary>The whole story has been shown and the game draws its Return to Menu button.</summary>
        public bool ReturnToMenu;
    }

    /// <summary>
    /// The story panel (the game's StoryPanel, on puzzle info, Options and Sigmar's Garden), as the
    /// game draws it: without a vignette, the periodic table quote and its author; with a
    /// one-section vignette (a journal puzzle's page), its texts; otherwise each dialogue line the
    /// save has reached, "Speaker: line", and the divider after a finished section ("Solve this
    /// puzzle to continue"), the last item where that section's condition is not met.
    /// </summary>
    public static class StoryPanelNodes
    {
        /// <param name="sections">The screen's storyPanelSections.</param>
        /// <param name="reached">What the screen hands RenderFrame as progress, compared with each section's count.</param>
        public static StoryView Read(StoryPanel panel, Tuple<int, LocString>[] sections, int reached)
        {
            var view = new StoryView();
            if (panel == null) return view;
            if (!panel.vignette.HasValue())
            {
                view.Items.Add(GameText.Clean(PeriodicTableScreen.quote));
                view.Items.Add(GameText.Clean(PeriodicTableScreen.name));
                return view;
            }
            var localized = panel.vignette.GetValue();
            var events = localized.GetVignette().events;
            if (events.Count == 1)
            {
                foreach (var text in panel.parsedText)
                    if (!string.IsNullOrWhiteSpace(text)) view.Items.Add(GameText.Clean(text));
                return view;
            }
            view.Dialogue = true;
            if (PuzzleSelectScreen.unlockAllMode) reached = int.MaxValue;
            int progress = GameLogic.instance.saveData.GetVignetteProgress(localized);
            int index = 0;
            for (int j = 0; j < sections.Length && j < events.Count; j++)
            {
                var list = events[j];
                for (int k = 0; k < list.Count; k++, index++)
                {
                    // Where the game stops: the first line of a section comes by itself after a
                    // moment, any other waits for a press.
                    if (index == progress)
                    {
                        view.More = k != 0;
                        return view;
                    }
                    if (!list[k].IsLineEvent()) continue;
                    var line = list[k].ToLineEvent();
                    view.Items.Add(GameText.Of(line.portrait.name) + ": " + GameText.Clean(line.line));
                }
                if (list.Count == 0) continue;
                if (j == sections.Length - 1)
                {
                    view.ReturnToMenu = localized.vignettePath != "solitaire" && localized.vignettePath != "options";
                    return view;
                }
                string divider = string.Format(Translations.Translate("{0} to continue").GetTranslated(), sections[j].Item2.GetTranslated());
                if (panel.addOptionalTag) divider += " " + Translations.Translate("(Optional)").GetTranslated();
                view.Items.Add(GameText.Clean(divider));
                if (reached < sections[j].Item1) return view;
            }
            return view;
        }
    }

    /// <summary>
    /// The story panel as a stop of puzzle info and Options: every item <see cref="StoryPanelNodes"/>
    /// reads, landing on the latest; Enter on any of them shows the next line, as the game's own
    /// Space does anywhere on the screen. Opening the game screen speaks the first line of dialogue
    /// (the whole of a quote or journal page); after that each new line as it appears, whether a
    /// press or the game's timer brought it. The latest item reads "More story" while the panel
    /// waits for a press, in the stop only, never spoken by itself (user decisions 2026-11-08: the
    /// game draws its "Click to advance the story" on the first lesson only, and a pulsing arrow
    /// elsewhere).
    /// </summary>
    internal sealed class StoryStop
    {
        public const string StopKey = "story";

        private readonly Func<StoryPanel> _panel;
        private readonly Func<Tuple<int, LocString>[]> _sections;
        private readonly Func<int> _reached;

        private int _opened; // the game screen this stop last opened on (its identity hash)
        private int _spoken; // items already spoken
        private string _lastError;

        public StoryStop(Func<StoryPanel> panel, Func<Tuple<int, LocString>[]> sections, Func<int> reached)
        {
            _panel = panel;
            _sections = sections;
            _reached = reached;
        }

        // Runs every frame, so a failure is logged once per distinct message.
        private StoryView Read()
        {
            try
            {
                return StoryPanelNodes.Read(_panel(), _sections(), _reached());
            }
            catch (Exception e)
            {
                if (e.Message != _lastError) CoreLog.Warning("[story] reading the story panel failed: " + e);
                _lastError = e.Message;
                return new StoryView();
            }
        }

        public void Build(GraphBuilder b)
        {
            var view = Read();
            if (view.Items.Count == 0) return;
            b.BeginStop(StopKey);
            b.PushContext(Strings.SolitaireStory, null, positions: false);
            for (int i = 0; i < view.Items.Count; i++)
            {
                int index = i;
                var id = ControlId.Structural("story:" + i);
                b.AddItem(id, new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[]
                    {
                        Nodes.LabelPart(() => { var items = Read().Items; return index < items.Count ? items[index] : ""; }),
                        new NodeAnnouncement(() => { var v = Read(); return v.More && index == v.Items.Count - 1 ? Strings.StoryMore : null; }, live: false, kind: AnnouncementKinds.Value),
                    },
                    OnActivate = Advance,
                });
                if (i == view.Items.Count - 1) b.SetStopStart(id);
            }
            if (view.ReturnToMenu)
                b.AddItem(ControlId.Structural("story:return"), Nodes.Button(() => GameText.Translate("Return to Menu"),
                    () => GameLogic.instance.PopScreen(Transitions.sink, MaybeHelper.empty)));
            b.PopContext();
        }

        private void Advance()
        {
            if (Read().More) GameKeys.Forced = GameKeys.Space;
        }

        /// <summary>From the screen's OnFocus. Coming back to the same game screen (from the editor,
        /// a message box) says nothing here: <see cref="Update"/> speaks whatever is new.</summary>
        public void Opened(object gameScreen)
        {
            if (gameScreen == null) return;
            int id = RuntimeHelpers.GetHashCode(gameScreen);
            if (id == _opened) return;
            _opened = id;
            var view = Read();
            int count = view.Dialogue ? Math.Min(1, view.Items.Count) : view.Items.Count;
            for (int i = 0; i < count; i++)
                if (!string.IsNullOrWhiteSpace(view.Items[i])) Speech.Say(view.Items[i], interrupt: false);
            _spoken = view.Items.Count;
        }

        /// <summary>From the screen's OnUpdate, every frame.</summary>
        public void Update()
        {
            var view = Read();
            if (view.Items.Count < _spoken) _spoken = view.Items.Count; // the save's progress went back
            for (int i = _spoken; i < view.Items.Count; i++)
                if (!string.IsNullOrWhiteSpace(view.Items[i])) Speech.Say(view.Items[i], interrupt: false);
            _spoken = Math.Max(_spoken, view.Items.Count);
            FinishScrollDelay();
        }

        // The game ignores an advance for 0.35 s after the last one, while the panel scrolls: one
        // press is one line here, however quickly it follows the last (the cutscenes' rule).
        private void FinishScrollDelay()
        {
            try
            {
                var panel = _panel();
                if (panel == null || !panel.scrollbarInfo.HasValue()) return;
                var info = panel.scrollbarInfo.GetValue();
                if (!info.isAfterInit && info.movementTimer <= StoryPanel.skipDelay)
                    info.movementTimer = StoryPanel.skipDelay + 0.01f;
            }
            catch (Exception e)
            {
                if (e.Message != _lastError) CoreLog.Warning("[story] the scroll delay: " + e);
                _lastError = e.Message;
            }
        }
    }
}
