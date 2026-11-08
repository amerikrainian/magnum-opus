using System.Collections.Generic;
using System.Linq;
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
    /// The Journal of Alchemical Engineering (JournalScreen: immediate-mode, mouse only). Two stops:
    ///  1. the issue (a dropdown of every issue, the game's previous / next arrows, the issue's text),
    ///     then its five puzzles, named as the game prints them with their author; a completed one
    ///     says so; Enter opens it as the game's click does (Sigmar's Garden II says what unlocks it);
    ///  2. the tabs and Close (the game's Escape). The pictures of the products are not described.
    /// </summary>
    public sealed class JournalGameScreen : GameScreen<JournalScreen>
    {
        public override string Key => "game.journal";

        public override string ScreenName => GameText.Translate("The Journal of Alchemical Engineering");

        private static SaveData Save => GameLogic.instance.saveData;
        private static JournalVolume Volume => JournalVolumes.volumes[Save.GetJournalVolume()];

        public override void Build(GraphBuilder b)
        {
            if (Game == null) return;

            b.BeginStop("issue");
            b.AddItem(ControlId.Structural("journal:issue"), Nodes.Dropdown(
                () => Strings.JournalIssue,
                () => GameText.Of(Volume.issueName),
                () => JournalVolumes.volumes.Select(v => GameText.Of(v.issueName)).ToList(),
                () => Save.GetJournalVolume(),
                i =>
                {
                    Save.SetJournalVolume(i);
                    Assets.sounds.click_button.PlaySound(1f);
                }));
            b.AddItem(ControlId.Structural("journal:prev"), Nodes.Button(() => Strings.JournalPrevious, () => Step(-1), enabled: () => Save.GetJournalVolume() > 0));
            b.AddItem(ControlId.Structural("journal:next"), Nodes.Button(() => Strings.JournalNext, () => Step(1), enabled: () => Save.GetJournalVolume() < JournalVolumes.volumes.Length - 1));
            b.AddItem(ControlId.Structural("journal:text"), Nodes.Text(() => GameText.Of(Volume.flavorText)));

            // By position, so the focus stays put when the issue changes.
            for (int i = 0; i < Volume.puzzles.Length; i++)
            {
                int slot = i;
                b.AddItem(ControlId.Structural("journal:puzzle:" + i), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new List<NodeAnnouncement>
                    {
                        Nodes.LabelPart(() => Title(Volume.puzzles[slot])),
                        new NodeAnnouncement(() => Locked(Volume.puzzles[slot]) ? GameText.Translate("Win 1 game of Sigmar's Garden to unlock") : null, kind: AnnouncementKinds.Enabled),
                        new NodeAnnouncement(() => Completed(Volume.puzzles[slot]) ? Strings.StateCompleted : null, live: true, kind: AnnouncementKinds.Selected),
                    },
                    SearchText = () => Title(Volume.puzzles[slot]),
                    OnActivate = () => Open(Volume.puzzles[slot]),
                });
            }

            b.BeginStop("tabs");
            b.AddItem(ControlId.Structural("journal:tab:journal"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement> { Nodes.LabelPart(() => GameText.Translate("The Journal")), Nodes.SelectedPart(() => true) },
                SearchText = () => GameText.Translate("The Journal"),
            });
            b.AddItem(ControlId.Structural("journal:tab:editor"), Nodes.Button(() => GameText.Translate("Puzzle Editor"), () => OpenEditor(PuzzleEditorScreenType.PersonalPuzzles)));
            b.AddItem(ControlId.Structural("journal:tab:workshop"), Nodes.Button(() => GameText.Translate("Steam Workshop"), () => OpenEditor(PuzzleEditorScreenType.WorkshopPuzzles)));
            b.AddItem(ControlId.Structural("journal:close"), Nodes.Button(() => Strings.ModClose, () => GameKeys.Forced = GameKeys.Escape));
        }

        private static bool IsSolitaire(Puzzle puzzle) => puzzle.puzzleId == Puzzles.sigmarsGardenII.puzzleId;

        private static string Title(Puzzle puzzle)
        {
            string name = GameText.Of(puzzle.puzzleName);
            return puzzle.journalAuthor.HasValue() ? name + " (" + GameText.Clean(puzzle.journalAuthor.GetValue()) + ")" : name;
        }

        private static bool Completed(Puzzle puzzle) =>
            IsSolitaire(puzzle) ? Save.GetSolitaireVariantWon() : Save.IsComplete(puzzle);

        private static bool Locked(Puzzle puzzle)
        {
            if (!IsSolitaire(puzzle) || !AppConsts.requireSteam) return false;
            Steamworks.SteamUserStats.GetStat("SOLITAIRE", out int won);
            return won == 0;
        }

        // RenderPuzzleSelect's click.
        private static void Open(Puzzle puzzle)
        {
            if (Locked(puzzle)) return;
            if (IsSolitaire(puzzle))
            {
                GameLogic.instance.PushScreen(new SolitaireScreen(SolitaireType.Quintessence));
            }
            else
            {
                var track = PuzzleEditorScreen.RandomTrack(puzzle);
                GameLogic.instance.PushScreen(new PuzzleInfoScreen(puzzle, track.Item1, track.Item2, MaybeHelper.empty));
            }
            Assets.sounds.click_button.PlaySound(1f);
        }

        // The game's arrows.
        private static void Step(int by)
        {
            Save.SetJournalVolume(Save.GetJournalVolume() + by);
            Assets.sounds.click_button.PlaySound(1f);
            Speech.Say(GameText.Of(Volume.issueName));
        }

        // MenuButtons' click, except that the game also makes this tab the one its Journal button
        // opens from then on: the puzzle editor is not operable yet, so that button stays the Journal.
        private static void OpenEditor(PuzzleEditorScreenType type)
        {
            GameLogic.instance.PopScreen();
            GameLogic.instance.PushScreen(new PuzzleEditorScreen(type));
            Assets.sounds.click_button.PlaySound(1f);
        }
    }

    /// <summary>
    /// The panel the game lays over the journal (JournalSubmissionScreen): the warning on a new
    /// player's first visits, the submission rules, or the list of issues. Continue and Escape close
    /// it the game's way (both answer Escape).
    /// </summary>
    public sealed class JournalSubmissionGameScreen : GameScreen<JournalSubmissionScreen>
    {
        public override string Key => "game.journal.panel";

        public override string ScreenName => Game != null && Game.type == JournalSubmissionScreen.Type.JournalIssueSelector ? Strings.JournalIssue : null;

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g == null) return;
            if (g.type == JournalSubmissionScreen.Type.JournalIssueSelector)
            {
                BuildIssues(b);
                return;
            }
            // The game draws the first section of the warning, all six of the submission rules.
            int shown = g.type == JournalSubmissionScreen.Type.SubmissionInfo ? 6 : 1;
            b.BeginStop("panel");
            for (int i = 0; i < shown && i < g.parsedText.Length; i++)
            {
                string line = GameText.Clean(g.parsedText[i]);
                b.AddItem(ControlId.Structural("panel:text:" + i), Nodes.Text(() => line));
            }
            b.AddItem(ControlId.Structural("panel:continue"), Nodes.Button(() => GameText.Translate("Continue"), () => GameKeys.Forced = GameKeys.Escape));
        }

        private static void BuildIssues(GraphBuilder b)
        {
            b.BeginStop("issues");
            var volumes = JournalVolumes.volumes;
            for (int i = 0; i < volumes.Length; i++)
            {
                int index = i;
                b.AddItem(ControlId.Structural("panel:issue:" + i), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new List<NodeAnnouncement>
                    {
                        Nodes.LabelPart(() => GameText.Of(volumes[index].issueName)),
                        Nodes.SelectedPart(() => GameLogic.instance.saveData.GetJournalVolume() == index),
                    },
                    SearchText = () => GameText.Of(volumes[index].issueName),
                    OnActivate = () =>
                    {
                        GameLogic.instance.saveData.SetJournalVolume(index);
                        GameLogic.instance.PopScreen();
                        Assets.sounds.click_button.PlaySound(1f);
                    },
                });
            }
            b.SetStopStart(ControlId.Structural("panel:issue:" + GameLogic.instance.saveData.GetJournalVolume()));
        }
    }
}
