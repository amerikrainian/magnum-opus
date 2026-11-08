using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The campaign map (the game's PuzzleSelectScreen: immediate-mode, mouse only). Three Tab stops:
    ///  1. the chapter's campaign items (puzzles, cutscenes, letters, solitaire): name, kind,
    ///     completed; a locked row (the game draws it blank) says "locked". For a campaign whose
    ///     chapters share the screen (De Re Metallica) every unlocked chapter is a group;
    ///  2. the chapter gems (the main campaign's chapters, locked or not, the current one selected;
    ///     none on a campaign that shows its chapters together);
    ///  3. Journal, the campaign switch, Options.
    /// Enter does what the click does, by the game's own code path (the same screen pushes with the
    /// same transitions and sounds; see the matching lines of PuzzleSelectScreen.RenderFrame and
    /// RenderChapter). Items do nothing while the unlock sequence plays (the game ignores clicks
    /// then too), and the sequence's events are spoken as the game plays them (it only sounds them).
    /// Escape passes to the game, which opens the options menu (EscapeScreen).
    /// </summary>
    public sealed class PuzzleSelectGameScreen : GameScreen<PuzzleSelectScreen>
    {
        public override string Key => "game.puzzleselect";

        // What the unlock sequence has played so far, to speak each new event once.
        private int _eventsSeen = -1;
        private PuzzleSelectScreen _seenFor;

        public override string ScreenName
        {
            get
            {
                var g = Game;
                return g == null ? null : GameText.Of(Shown(g).locationName);
            }
        }

        private static CampaignChapter Shown(PuzzleSelectScreen g) =>
            g.newChapter.HasValue() && g.chapterTransitionTimer >= 0.5f ? g.newChapter.GetValue() : g.currentChapter;

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g == null) return;
            var chapter = Shown(g);
            var campaign = chapter.campaign;

            b.BeginStop("items");
            if (campaign.sameScreenChapters)
            {
                foreach (var c in campaign.chapters.Where(g.IsUnlocked))
                    Chapter(b, g, c);
            }
            else
            {
                Chapter(b, g, chapter);
            }

            // The game slides the gems off the screen on a campaign that shows its chapters together.
            if (!campaign.sameScreenChapters)
            {
                b.BeginStop("chapters");
                b.PushContext(Strings.SelectChapters, Strings.RoleList);
                foreach (var c in Campaigns.opusMagnum.chapters)
                {
                    var ch = c;
                    b.AddItem(ControlId.Structural("chapter:" + ch.chapterNumber), new NodeVtable
                    {
                        ControlType = Core.UI.ControlTypes.Button,
                        Announcements = new List<NodeAnnouncement>
                        {
                            Nodes.LabelPart(() => Strings.SelectChapter(GameText.Of(ch.title), GameText.Of(ch.name))),
                            Nodes.SelectedPart(() => Game != null && Shown(Game) == ch),
                            new NodeAnnouncement(() => Game != null && !Game.IsUnlocked(ch) ? Strings.StateLocked : null, kind: AnnouncementKinds.Enabled),
                        },
                        SearchText = () => GameText.Of(ch.name),
                        OnActivate = () => SelectChapter(ch),
                    });
                }
                b.PopContext();
            }

            b.BeginStop("actions");
            b.AddItem(ControlId.Structural("select:journal"), Nodes.Button(() => Strings.SelectJournal, OpenJournal));
            b.AddItem(ControlId.Structural("select:campaign"), Nodes.Button(CampaignSwitchLabel, SwitchCampaign,
                detail: () => Game != null && Game.currentChapter.campaign == Campaigns.opusMagnum && !PuzzleSelectScreen.HasDRM() ? Strings.DlcNotOwned : null));
            b.AddItem(ControlId.Structural("select:options"), Nodes.Button(() => GameText.Translate("Options"), OpenOptions));
        }

        private void Chapter(GraphBuilder b, PuzzleSelectScreen g, CampaignChapter chapter)
        {
            b.PushContext(Strings.SelectChapter(GameText.Of(chapter.title), GameText.Of(chapter.name)), Strings.RoleList);
            for (int i = 0; i < chapter.campaignItems.Count; i++)
            {
                var item = chapter.campaignItems[i];
                b.AddItem(ControlId.Structural("item:" + item.itemId), new NodeVtable
                {
                    ControlType = Core.UI.ControlTypes.Button,
                    Announcements = new List<NodeAnnouncement>
                    {
                        Nodes.LabelPart(() => IsUnlocked(item) ? GameText.Of(item.itemName) : Strings.StateLocked),
                        new NodeAnnouncement(() => IsUnlocked(item) ? Kind(item) : null, kind: AnnouncementKinds.Value),
                        new NodeAnnouncement(() => IsUnlocked(item) && IsCompleted(item) ? Strings.StateCompleted : null, live: true, kind: AnnouncementKinds.Selected),
                    },
                    SearchText = () => IsUnlocked(item) ? GameText.Of(item.itemName) : null,
                    OnActivate = () => Open(item),
                });
            }
            b.PopContext();
        }

        private static string Kind(CampaignItem item)
        {
            switch (item.itemType)
            {
                case CampaignItemType.Puzzle: return Strings.ItemKind("puzzle");
                case CampaignItemType.Cutscene: return Strings.ItemKind("cutscene");
                case CampaignItemType.Letter: return Strings.ItemKind("letter");
                case CampaignItemType.Solitaire: return Strings.ItemKind("solitaire");
                default: return null;
            }
        }

        private bool IsUnlocked(CampaignItem item)
        {
            var g = Game;
            return g != null && (PuzzleSelectScreen.unlockAllMode
                || g.completedPuzzleSelectEvents.OfType<CampaignItemUnlockEvent>().Any(e => e.campaignItem == item));
        }

        private bool IsCompleted(CampaignItem item)
        {
            var g = Game;
            return g != null && (PuzzleSelectScreen.unlockAllMode
                || g.completedPuzzleSelectEvents.OfType<CampaignItemCompleteEvent>().Any(e => e.campaignItem == item));
        }

        // RenderChapter's click: the item's screen, pushed as the game pushes it.
        private void Open(CampaignItem item)
        {
            var g = Game;
            if (g == null || !IsUnlocked(item) || g.eventQueue.Count != 0) return;
            switch (item.itemType)
            {
                case CampaignItemType.Puzzle:
                    GameLogic.instance.PushScreen(new PuzzleInfoScreen(item.puzzle.GetValue(), item.musicTrack, item.fanfare, item.vignette), MaybeHelper.empty, Transitions.raise);
                    break;
                case CampaignItemType.Cutscene:
                    GameLogic.instance.PushScreen(new CinematicScreen(item.vignette.GetValue(), item.musicTrack), Transitions.fromBlack, Transitions.toBlack);
                    Assets.sounds.click_button.PlaySound(1f);
                    break;
                case CampaignItemType.Letter:
                    GameLogic.instance.PushScreen(new DocumentScreen(item.vignette.GetValue()));
                    Assets.sounds.ui_paper.PlaySound(1f);
                    break;
                case CampaignItemType.Solitaire:
                    GameLogic.instance.PushScreen(new SolitaireScreen(item.GetSolitaireType()), MaybeHelper.empty, Transitions.raise);
                    break;
            }
        }

        // The chapter gem's click.
        private void SelectChapter(CampaignChapter chapter)
        {
            var g = Game;
            if (g == null || !g.IsUnlocked(chapter) || g.newChapter.HasValue() || chapter == g.currentChapter) return;
            g.newChapter = chapter;
            g.chapterTransitionTimer = 0f;
            g.currentCampaignNotOM = false;
            GameLogic.instance.saveData.SetDefaultChapter(chapter.chapterNumber);
            Assets.sounds.click_button.PlaySound(1f);
        }

        private string CampaignSwitchLabel()
        {
            var g = Game;
            bool onMain = g == null || g.currentChapter.campaign == Campaigns.opusMagnum;
            return Strings.SwitchCampaign(onMain ? GameText.Translate("De Re Metallica") : Strings.CampaignOpusMagnum);
        }

        // The campaign switch's click.
        private void SwitchCampaign()
        {
            var g = Game;
            if (g == null || g.newChapter.HasValue()) return;
            if (g.currentChapter.campaign == Campaigns.opusMagnum)
            {
                if (PuzzleSelectScreen.HasDRM())
                {
                    g.newChapter = Campaigns.deReMetallica.chapters[0];
                    g.chapterTransitionTimer = 0f;
                    g.currentCampaignNotOM = false;
                    Steamworks.SteamFriends.SetRichPresence("steam_display", "#DLC");
                }
                else
                {
                    PuzzleSelectScreen.ToDRMStore();
                }
            }
            else
            {
                g.newChapter = Campaigns.opusMagnum.chapters[GameLogic.instance.saveData.GetDefaultChapter()];
                g.chapterTransitionTimer = 0f;
                g.currentCampaignNotOM = false;
                Steamworks.SteamFriends.ClearRichPresence();
            }
            Assets.sounds.click_button.PlaySound(1f);
        }

        // The journal button's click (the game opens one of three screens by a mode field).
        private static void OpenJournal()
        {
            switch (GameLogic.instance.field_2466)
            {
                default:
                    GameLogic.instance.PushScreen(new JournalScreen(showDifficultyWarningIfNeeded: true), MaybeHelper.empty, Transitions.raise);
                    break;
                case 1:
                    GameLogic.instance.PushScreen(new PuzzleEditorScreen(PuzzleEditorScreenType.PersonalPuzzles), MaybeHelper.empty, Transitions.raise);
                    break;
                case 2:
                    GameLogic.instance.PushScreen(new PuzzleEditorScreen(PuzzleEditorScreenType.WorkshopPuzzles), MaybeHelper.empty, Transitions.raise);
                    break;
            }
            Assets.sounds.click_button.PlaySound(1f);
        }

        // The options button's click (also the game's own Escape).
        private static void OpenOptions()
        {
            GameLogic.instance.PushScreen(new EscapeScreen());
            Assets.sounds.ui_modal.PlaySound(1f);
        }

        public override void OnUpdate()
        {
            var g = Game;
            if (g == null) return;
            if (!ReferenceEquals(g, _seenFor)) { _seenFor = g; _eventsSeen = g.completedPuzzleSelectEvents.Count; return; }
            int count = g.completedPuzzleSelectEvents.Count;
            if (count <= _eventsSeen) { _eventsSeen = count; return; }
            // The set gained events: the unlock sequence played one. Speak the newest kinds.
            foreach (var e in g.completedPuzzleSelectEvents.Skip(_eventsSeen))
                Speak(e);
            _eventsSeen = count;
        }

        private static void Speak(PuzzleSelectEvent e)
        {
            switch (e)
            {
                case CampaignItemUnlockEvent u: Speech.Say(Strings.SelectUnlocked(GameText.Of(u.campaignItem.itemName))); break;
                case CampaignItemCompleteEvent c: Speech.Say(Strings.SelectCompleted(GameText.Of(c.campaignItem.itemName))); break;
                case ChapterUnlockEvent ch: Speech.Say(Strings.SelectChapterUnlocked(Strings.SelectChapter(GameText.Of(ch.chapter.title), GameText.Of(ch.chapter.name)))); break;
            }
        }
    }
}
