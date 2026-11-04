using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The pause menu (the game's EscapeScreen, titled "Menu"): Options, Controls, Exit Game, and the
    /// close button (Escape too: the game's CloseButton accepts it). Each button runs the game's
    /// own click code.
    /// </summary>
    public sealed class EscapeGameScreen : GameScreen<EscapeScreen>
    {
        public override string Key => "game.menu";
        public override string ScreenName => GameText.Translate("Menu");

        public override void Build(GraphBuilder b)
        {
            b.AddItem(ControlId.Structural("menu:options"), Nodes.Button(() => GameText.Translate("Options"), () =>
                GameLogic.instance.PushScreen(new OptionsScreen(), MaybeHelper.empty, Transitions.raise)));
            b.AddItem(ControlId.Structural("menu:controls"), Nodes.Button(() => GameText.Translate("Controls"), () =>
            {
                GameLogic.instance.PushScreen(new ControlsScreen(), MaybeHelper.empty, MaybeHelper.empty);
                Assets.sounds.ui_modal.PlaySound(1f);
            }));
            b.AddItem(ControlId.Structural("menu:exit"), Nodes.Button(() => GameText.Translate("Exit Game"), () => GameLogic.instance.GameUnload(0)));
            b.AddItem(ControlId.Structural("menu:close"), Nodes.Button(() => Strings.ModClose, () =>
            {
                GameLogic.instance.fadeBackground = false;
                GameLogic.instance.PopScreen();
                Assets.sounds.ui_modal_close.PlaySound(1f);
            }));
        }
    }

    /// <summary>
    /// Options (the game's OptionsScreen), over the settings themselves rather than the drawn
    /// widgets: Language, Resolution and Keyboard Layout are real combo boxes (Enter opens the list
    /// on the current value), the on/off settings toggles, the two volumes sliders (Left/Right in
    /// tenths). Every change does exactly what the game's own widget does (the same settings write,
    /// the same follow-up: the live language, the window re-apply flag, the volume, the click
    /// sound). Show Top Percentile stays disabled until the game is complete, as in the game. A
    /// second stop reads the story panel. Escape passes to the game, which closes the screen.
    /// </summary>
    public sealed class OptionsGameScreen : GameScreen<OptionsScreen>
    {
        public override string Key => "game.options";
        public override string ScreenName => GameText.Translate("Options");

        private static global::Settings Settings => GameLogic.instance.settingsData;

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g == null) return;

            b.BeginStop("settings");
            b.AddItem(ControlId.Structural("opt:language"), Nodes.Dropdown(
                () => GameText.Translate("Language"),
                () => GameText.Clean(Translations.languageNames[Settings.language.Get()]),
                () => OptionsScreen.GetLanguages().Select(l => GameText.Clean(Translations.languageNames[l])).ToList(),
                () => OptionsScreen.GetLanguages().IndexOf(Settings.language.Get()),
                i =>
                {
                    var language = OptionsScreen.GetLanguages()[i];
                    Settings.language.Set(language);
                    Translations.currentLanguage = language;
                    Assets.sounds.click_button.PlaySound(1f);
                }));

            b.AddItem(ControlId.Structural("opt:resolution"), Nodes.Dropdown(
                () => GameText.Translate("Resolution"),
                () => Resolution(Settings.GetResolution()),
                () => Resolutions().Select(Resolution).ToList(),
                () => Resolutions().IndexOf(Settings.GetResolution()),
                i =>
                {
                    Settings.SetResolution(Resolutions()[i]);
                    GameLogic.instance.field_2440 = true;
                    Assets.sounds.click_button.PlaySound(1f);
                }));

            b.AddItem(ControlId.Structural("opt:fullscreen"), Nodes.Toggle(() => GameText.Translate("Fullscreen"),
                () => Settings.fullscreen.Get(), v =>
                {
                    Settings.fullscreen.Set(v);
                    GameLogic.instance.field_2440 = true;
                    Assets.sounds.click_button.PlaySound(1f);
                }));

            b.AddItem(ControlId.Structural("opt:sound"), Volume(() => GameText.Translate("Sound Volume"),
                () => Settings.volumeSound.Get(), v => { Settings.volumeSound.Set(v); PlatformBridge.soundVolume = v; }));
            b.AddItem(ControlId.Structural("opt:music"), Volume(() => GameText.Translate("Music Volume"),
                () => Settings.volumeMusic.Get(), v => { Settings.volumeMusic.Set(v); PlatformBridge.musicVolume = v; }));

            b.AddItem(ControlId.Structural("opt:layout"), Nodes.Dropdown(
                () => GameText.Translate("Keyboard Layout"),
                () => GameText.Of(Keybinds.keyboardLayouts[Settings.keyboardLayout.Get()]),
                () => Layouts.Select(l => GameText.Of(Keybinds.keyboardLayouts[l])).ToList(),
                () => Array.IndexOf(Layouts, Settings.keyboardLayout.Get()),
                i =>
                {
                    Settings.keyboardLayout.Set(Layouts[i]);
                    Keybinds.currentLayout = Layouts[i];
                    Assets.sounds.click_button.PlaySound(1f);
                }));

            b.AddItem(ControlId.Structural("opt:hotkeys"), WithDetail(Nodes.Toggle(() => GameText.Translate("Quick Hotkeys"),
                () => Settings.quickHotkeys.Get(), v => { Settings.quickHotkeys.Set(v); Assets.sounds.click_button.PlaySound(1f); }),
                () => GameText.Translate("Pressing a key places an instruction")));
            b.AddItem(ControlId.Structural("opt:hermit"), WithDetail(Nodes.Toggle(() => GameText.Translate("Hermit Mode"),
                () => Settings.enableHermitMode.Get(), v => { Settings.enableHermitMode.Set(v); Assets.sounds.click_button.PlaySound(1f); }),
                () => GameText.Translate("Hides leaderboards and histograms")));
            b.AddItem(ControlId.Structural("opt:percentile"), WithDetail(Nodes.Toggle(() => GameText.Translate("Show Top Percentile"),
                () => Settings.showGlobalRecords.Get(), v => { Settings.showGlobalRecords.Set(v); Assets.sounds.click_button.PlaySound(1f); },
                enabled: GameComplete),
                () => GameComplete() ? null : GameText.Translate("Complete the game to unlock")));

            b.AddItem(ControlId.Structural("opt:reverify"), Nodes.Button(() => GameText.Translate("Reverify Solutions"), Reverify,
                detail: () => GameText.Translate("Resubmits scores to the leaderboards")));
            b.AddItem(ControlId.Structural("opt:gif"), Nodes.Text(() => GameText.Translate("GIF Output Folder") + ": " + SolutionRecorderScreen.GetGifOutputDirectory()));
            b.AddItem(ControlId.Structural("opt:gif-set"), Nodes.Button(() => GameText.Translate("Set"), () =>
            {
                var picked = DirectorySelector.SelectDirectory();
                if (picked.HasValue() && System.IO.Directory.Exists(picked.GetValue()))
                    Settings.gifOutputDirectory.Set(picked.GetValue());
                Assets.sounds.click_button.PlaySound(1f);
            }));
            b.AddItem(ControlId.Structural("opt:gif-reset"), Nodes.Button(() => GameText.Translate("Reset"), () =>
            {
                Settings.gifOutputDirectory.Set(string.Empty);
                Assets.sounds.click_button.PlaySound(1f);
            }));
            b.AddItem(ControlId.Structural("opt:close"), Nodes.Button(() => Strings.ModClose, () =>
                GameLogic.instance.PopScreen(Transitions.sink, MaybeHelper.empty)));

            b.BeginStop("story");
            StoryPanelNodes.Build(b, "opt:story", () => Game?.storyPanel);
        }

        private static readonly KeyboardLayout[] Layouts =
        {
            KeyboardLayout.Qwerty, KeyboardLayout.Qwertz, KeyboardLayout.Azerty,
            KeyboardLayout.Dvorak, KeyboardLayout.Colemak, KeyboardLayout.Numpad,
        };

        private static List<Index2> Resolutions() => OptionsScreen.GetScreenResolutions(Settings.fullscreen.Get());

        private static string Resolution(Index2 r) => r.X + " x " + r.Y;

        private static bool GameComplete() => GameLogic.instance.saveData.IsComplete(Puzzles.universalSolvent);

        private static NodeVtable WithDetail(NodeVtable node, Func<string> detail)
        {
            var parts = new List<NodeAnnouncement>(node.Announcements) { Nodes.TooltipPart(detail) };
            node.Announcements = parts;
            return node;
        }

        // A volume as a percentage, stepped in tenths (quarters with the large step).
        private static NodeVtable Volume(Func<string> label, Func<float> get, Action<float> set)
        {
            return Nodes.Slider(label, () => (int)Math.Round(get() * 100) + "%", (sign, large) =>
            {
                float step = large ? 0.25f : 0.1f;
                float v = (float)Math.Round(Math.Max(0f, Math.Min(1f, get() + sign * step)), 2);
                set(v);
                Assets.sounds.click_button.PlaySound(1f);
            });
        }

        // The Reverify Solutions button's click.
        private static void Reverify()
        {
            foreach (Puzzle puzzle in Puzzles.GetAllPuzzles())
            {
                GameLogic.instance.saveData.RemovePuzzleBest(puzzle, ScoreMetric.Cost);
                GameLogic.instance.saveData.RemovePuzzleBest(puzzle, ScoreMetric.Cycles);
                GameLogic.instance.saveData.RemovePuzzleBest(puzzle, ScoreMetric.Footprint);
                GameLogic.instance.saveData.RemovePuzzleBest(puzzle, ScoreMetric.Instructions);
            }
            foreach (Solution solution in GameLogic.instance.globalSolutions)
            {
                GameLogic.instance.solutionQueue.Enqueue(solution);
                Assets.sounds.click_button.PlaySound(1f);
            }
        }
    }
}
