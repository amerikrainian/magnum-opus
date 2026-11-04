using System;
using System.Text;
using HarmonyLib;
using MagnumOpus.Contracts;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Input;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Host;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.Screens;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module
{
    // The game's own InputManager (global namespace) shadows Core's otherwise.
    using InputManager = MagnumOpus.Core.Input.InputManager;

    /// <summary>
    /// The reloadable feature module's composition root: wires Core's seams to the host, registers
    /// the input actions and screens, owns focus mode, and drives the per-frame loop from the host's
    /// GameLoop prefix. Game patches arm on the FIRST TICK, never in Load: patching a game method runs
    /// its type's static constructor, and Load runs before the game's own init (the Echopunks
    /// cold-boot lesson). Everything is undone in Dispose so a fresh generation can swap in.
    /// </summary>
    public sealed class ModuleMain : IModModule, IDevDriver
    {
        private IModHost _host;
        private ModHostImpl _impl;
        private Harmony _harmony;
        private bool _armed;

        /// <summary>The host of the live generation (settings, logging), for screens that need it.</summary>
        public static IModHost Host { get; private set; }

        public void Load(IModHost host)
        {
            _host = host;
            _impl = host as ModHostImpl;
            Host = host;

            CoreLog.Info = host.LogInfo;
            CoreLog.Warning = host.LogWarning;
            CoreLog.Error = host.LogError;
            Speech.Speak = (text, interrupt) => host.Speech.Speak(text, interrupt);
            NavInput.Current = new SdlNavInput();
            Navigation.FocusActive = () => FocusMode.Active;
            InputManager.FocusActive = () => FocusMode.Active;
            GraphAnnouncer.PositionText = (i, n) => host.Settings.SpeakPositions ? Strings.Position(i, n) : null;
            GraphAnnouncer.ExpandedStateText = Strings.ExpandedState;
            InputBinding.RegisterType("keyboard", KeyboardBinding.Deserialize);

            // A per-load UNIQUE id: the host loads the new module before disposing the old one, and
            // UnpatchSelf removes by id, so a fixed id would let the old teardown strip the new patches.
            _harmony = new Harmony("com.magnumopus.module." + Guid.NewGuid().ToString("N"));

            LanguageSync.Start(host.PluginDir);
            RegisterInput();
            RegisterScreens();
            FocusMode.Set(host.Settings.FocusModeOnLaunch);
            host.LogInfo("[module] loaded, generation " + (_impl?.Generation ?? 0) + ": " + ScreenManager.Registered.Count
                + " screens, " + InputManager.Actions.Count + " input actions, focus " + (FocusMode.Active ? "on" : "off"));
        }

        private static void RegisterInput()
        {
            // UI navigation keys: live while a screen declares the UI category and focus mode is on.
            InputManager.Register(UiActions.Up, "Navigate up", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.Up)).Repeating();
            InputManager.Register(UiActions.Down, "Navigate down", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.Down)).Repeating();
            InputManager.Register(UiActions.Left, "Navigate left", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.Left)).Repeating();
            InputManager.Register(UiActions.Right, "Navigate right", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.Right)).Repeating();
            InputManager.Register(UiActions.Next, "Next control group", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.Tab)).Repeating();
            InputManager.Register(UiActions.Prev, "Previous control group", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.Tab, shift: true)).Repeating();
            InputManager.Register(UiActions.Activate, "Activate", InputCategory.UI)
                .AddBinding(new KeyboardBinding(Scancode.Return)).AddBinding(new KeyboardBinding(Scancode.KpEnter));
            InputManager.Register(UiActions.Secondary, "Secondary action", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.Backspace));
            InputManager.Register(UiActions.Back, "Back", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.Escape));
            InputManager.Register(UiActions.Home, "Jump to first", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.Home));
            InputManager.Register(UiActions.End, "Jump to last", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.End));
            InputManager.Register(UiActions.RegionPrev, "Previous section", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.Up, alt: true));
            InputManager.Register(UiActions.RegionNext, "Next section", InputCategory.UI).AddBinding(new KeyboardBinding(Scancode.Down, alt: true));

            // Global: always live, so the player can hand the keyboard back to the game and reclaim it.
            InputManager.Register("mod.focus", "Toggle navigation", InputCategory.Global, ToggleFocus)
                .AddBinding(new KeyboardBinding(Scancode.A, ctrl: true, shift: true));
            InputManager.Register("mod.menu", "Mod menu", InputCategory.Global, ModMenuScreen.Toggle)
                .AddBinding(new KeyboardBinding(Scancode.M, ctrl: true, shift: true));
            InputManager.Register(HelpScreen.ActionKey, "Keys here", InputCategory.Global, HelpScreen.Toggle)
                .AddBinding(new KeyboardBinding(Scancode.F1));
#if DEBUG
            // Reload Core and the module from disk (the dev loop; also POST /reload).
            InputManager.Register("mod.reload", "Reload mod code", InputCategory.Global, () => Runtime.Post(() => Runtime.ReloadModule()))
                .AddBinding(new KeyboardBinding(Scancode.F6));
#endif
        }

        private static void ToggleFocus()
        {
            FocusMode.Toggle();
            Speech.Say(FocusMode.Active ? Strings.FocusOn : Strings.FocusOff, interrupt: true);
            if (FocusMode.Active) Navigation.AnnounceCurrent();
        }

        private static void RegisterScreens()
        {
            ScreenManager.Register(new TitleGateScreen());
            ScreenManager.Register(new PuzzleSelectGameScreen());
            ScreenManager.Register(new DocumentGameScreen());
            ScreenManager.Register(new EscapeGameScreen());
            ScreenManager.Register(new OptionsGameScreen());
            ScreenManager.Register(new ControlsGameScreen());
            ScreenManager.Register(new MessageBoxGameScreen());
            ScreenManager.Register(new PuzzleInfoGameScreen());
            ScreenManager.Register(new CinematicGameScreen());
            ScreenManager.Register(new UploadGameScreen());
            ScreenManager.Register(new ModMenuScreen());
            ScreenManager.Register(new HelpScreen());
        }

        public void Tick()
        {
            SdlKeyboard.Update();
            Patches.GameKeys.Forced = 0; // a synthesized press lasts one game frame
            Patches.ButtonPress.NewFrame();
            // A dev-driver key the game would see if it were real: forward it (the game reads its
            // own SDL events and never sees an injection).
            int injected = SdlKeyboard.InjectedGameKey;
            if (injected != 0 && Patches.GameKeys.GameSees(injected)) Patches.GameKeys.Forced = injected;
            if (!_armed) Arm();
            LanguageSync.Tick(); // before anything speaks this frame
            Announce();
            InputManager.Tick();
            ScreenManager.Tick();
            Navigation.TickTypeahead();
            Safe(Later.Tick, "later");
        }

        // The game is initialized by the first tick (the GameLoop prefix): patching is safe now.
        private void Arm()
        {
            _armed = true;
            try { Patches.GameKeys.Apply(_harmony); Patches.ButtonPress.Apply(_harmony); }
            catch (Exception e) { _host.LogError("[module] arming game patches failed: " + e); }
        }

        private void Announce()
        {
            if (!_host.LaunchAnnounced)
            {
                _host.LaunchAnnounced = true;
                Speech.Say(Strings.ModLoaded(_host.ModVersion));
                // Matched names announce their own count (with the notice); with a community mapping
                // an unresolved reference is a mod bug, said all the same.
                if (_impl != null && _impl.NameSource != "matched" && _impl.Unresolved.Count > 0)
                    Speech.Say(Strings.Unmatched(_impl.Unresolved.Count));
            }
            while (ModHostImpl.Notices.TryDequeue(out var notice))
            {
                switch (notice)
                {
                    case HostNotice.NamesMatched: Speech.Say(Strings.NamesMatched); break;
                    case HostNotice.MappingDownloading: Speech.Say(Strings.MappingDownloading); break;
                    case HostNotice.MappingNotPublished: Speech.Say(Strings.MappingNotPublished); break;
                    case HostNotice.MappingDownloadFailed: Speech.Say(Strings.MappingFailed); break;
                    case HostNotice.MappingUpdated: Speech.Say(Strings.MappingUpdated); break;
                }
                if ((notice == HostNotice.NamesMatched || notice == HostNotice.MappingUpdated) && _impl != null && _impl.Unresolved.Count > 0)
                    Speech.Say(Strings.Unmatched(_impl.Unresolved.Count));
            }
        }

        private void Safe(Action tick, string what)
        {
            try { tick(); }
            catch (Exception e) { _host?.LogWarning("[" + what + "] " + e); }
        }

        public void Dispose()
        {
            try { ScreenManager.Shutdown(); } catch (Exception e) { _host?.LogError("[dispose] screens: " + e); }
            try { InputManager.Clear(); } catch (Exception e) { _host?.LogError("[dispose] input: " + e); }
            try { _harmony?.UnpatchAll(_harmony.Id); } catch (Exception e) { _host?.LogError("[dispose] harmony: " + e); }
            _harmony = null;
            _host = null;
        }

        // ---- IDevDriver: the dev server drives and inspects our navigation through these ----

        public string DispatchAction(string actionKey)
        {
            if (actionKey != null && actionKey.StartsWith("dev.type:", StringComparison.Ordinal))
            {
                SdlNavInput.InjectTyped(actionKey.Substring("dev.type:".Length));
                return "typing next frame";
            }
            // A key chord pressed on the next frame through the mod's own keyboard snapshot
            // (dev.key:down, dev.key:ctrl+shift+m): no OS input, no window focus.
            if (actionKey != null && actionKey.StartsWith("dev.key:", StringComparison.Ordinal))
                return InjectChord(actionKey.Substring("dev.key:".Length));
            if (string.IsNullOrEmpty(actionKey) || InputManager.Find(actionKey) == null) return null;
            InputManager.Dispatch(actionKey);
            var node = (Navigation.Active as GraphNavigator)?.FocusedNode;
            return "fired " + actionKey + (node != null ? " -> " + GraphAnnouncer.ComposeFull(node) : "");
        }

        private static string InjectChord(string chord)
        {
            var codes = new System.Collections.Generic.List<int>();
            foreach (var raw in chord.Split('+'))
            {
                string part = raw.Trim().ToLowerInvariant();
                switch (part)
                {
                    case "ctrl": codes.Add(224); continue;
                    case "shift": codes.Add(225); continue;
                    case "alt": codes.Add(226); continue;
                    case "enter": part = "return"; break;
                    case "esc": part = "escape"; break;
                }
                if (part.Length == 1 && char.IsDigit(part[0])) part = "num" + part;
                if (!Enum.TryParse(part, true, out Scancode sc)) return "[unknown key] " + raw;
                codes.Add((int)sc);
            }
            SdlKeyboard.Inject(codes.ToArray());
            return "pressing " + chord + " next frame";
        }

        public string DescribeNav()
        {
            var sb = new StringBuilder();
            sb.Append("focus mode: ").Append(FocusMode.Active ? "on" : "off").Append('\n');
            sb.Append("game screen: ").Append(Game.GameScreens.Name(Game.GameScreens.Top)).Append('\n');
            sb.Append("stack: ");
            foreach (var s in ScreenManager.Stack) sb.Append(s.Key).Append(' ');
            sb.Append('\n');
            var cur = ScreenManager.Current;
            sb.Append("screen: ").Append(cur != null ? cur.Key + " (" + (cur.ScreenName ?? "") + ")" : "(none)").Append('\n');
            var nav = Navigation.Active as GraphNavigator;
            var node = nav?.FocusedNode;
            if (node == null) return sb.Append("focus: (none)\n").ToString();
            sb.Append("focus: ").Append(node.Id).Append('\n');
            sb.Append("readout: ").Append(GraphAnnouncer.ComposeFull(node)).Append('\n');
            var render = nav.CurrentRender;
            if (render != null)
            {
                sb.Append("nodes (").Append(render.Order.Count).Append("):\n");
                foreach (var n in render.Order)
                    sb.Append(ReferenceEquals(n, node) ? "  > " : "    ").Append(n.Id.StructuralKey).Append(": ").Append(GraphAnnouncer.LeafText(n)).Append('\n');
            }
            return sb.ToString();
        }

        public string ListActions()
        {
            var sb = new StringBuilder();
            foreach (var a in InputManager.Actions)
                sb.Append(a.Key).Append(" [").Append(a.Category).Append("] ").Append(a.BindingsDisplay).Append('\n');
            return sb.ToString();
        }
    }
}
