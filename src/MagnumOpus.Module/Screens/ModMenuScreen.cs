using System;
using System.Collections.Generic;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Input;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.UI;
using Screen = MagnumOpus.Core.Screens.Screen;

namespace MagnumOpus.Module.Screens
{
    // The game's own InputManager (global namespace) shadows Core's otherwise.
    using InputManager = MagnumOpus.Core.Input.InputManager;

    /// <summary>
    /// The mod's own menu (Ctrl+Shift+M anywhere): Settings, All keys, Close. An overlay above every
    /// game screen, opened and closed by a static flag the poll picks up; its sub-screens are child
    /// screens, so Escape walks back through them to the game.
    /// </summary>
    public sealed class ModMenuScreen : Screen
    {
        private static bool s_open;

        // The landing reads "Magnum Opus, list, Settings, button": the menu's context carries its
        // title, so opening says nothing of its own.
        public static void Toggle() => s_open = !s_open;

        public static void Close() => s_open = false;

        public override string Key => "mod.menu";
        public override int Layer => 50;
        public override bool Exclusive => true;
        public override bool IsActive() => s_open;

        public override void Build(GraphBuilder b)
        {
            b.PushContext(Strings.ScreenModMenu, Strings.RoleList);
            b.AddItem(ControlId.Structural("modmenu:settings"), Nodes.Button(() => Strings.ModSettings, () => PushChild(new ModSettingsScreen())));
            b.AddItem(ControlId.Structural("modmenu:keys"), Nodes.Button(() => Strings.ModKeyHelp, () => PushChild(new KeyListScreen())));
            b.AddItem(ControlId.Structural("modmenu:close"), Nodes.Button(() => Strings.ModClose, Close));
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Close());
        }
    }

    /// <summary>The mod's settings, kept in the host's settings.json so they outlive reloads.</summary>
    public sealed class ModSettingsScreen : Screen
    {
        public override string Key => "mod.settings";
        public override string ScreenName => Strings.ModSettings;
        public override bool IsActive() => false; // only ever a child of the mod menu

        public override void Build(GraphBuilder b)
        {
            var settings = ModuleMain.Host?.Settings;
            b.PushContext(Strings.ModSettings, Strings.RoleList);
            if (settings != null)
            {
                b.AddItem(ControlId.Structural("modsettings:positions"), Nodes.Toggle(() => Strings.ModSpeakPositions,
                    () => settings.SpeakPositions, v => settings.SpeakPositions = v));
                b.AddItem(ControlId.Structural("modsettings:focus"), Nodes.Toggle(() => Strings.ModFocusOnLaunch,
                    () => settings.FocusModeOnLaunch, v => settings.FocusModeOnLaunch = v));
                b.AddItem(ControlId.Structural("modsettings:short_directions"), Nodes.Toggle(() => Strings.ModShortDirections,
                    () => Prefs.ShortDirections, v => Prefs.ShortDirections = v));
                b.AddItem(ControlId.Structural("modsettings:interrupt_playing"), Nodes.Toggle(() => Strings.ModInterruptWhilePlaying,
                    () => Prefs.InterruptWhilePlaying, v => Prefs.InterruptWhilePlaying = v));
                b.AddItem(ControlId.Structural("modsettings:arm_change_to_end"), Nodes.Toggle(() => Strings.ModArmChangeToEnd,
                    () => Prefs.ArmChangeToEnd, v => Prefs.ArmChangeToEnd = v));
                b.AddItem(ControlId.Structural("modsettings:arm_wrap"), Nodes.Toggle(() => Strings.ModArmWrap,
                    () => Prefs.ArmWrap, v => Prefs.ArmWrap = v));
            }
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ParentScreen?.RemoveChild(this));
        }
    }
}
