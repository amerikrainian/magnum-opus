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
    /// "Keys here" (F1 anywhere): the keys that would do something right now, for the screen and the
    /// control the player stood on when they asked (<see cref="KeyHelp"/>), each row what the key does
    /// and the keys bound to it. The list is taken when the help opens, while the game screen still
    /// has the focus. Enter on a row runs the action as the key would have: the help closes, the
    /// action is dispatched through the real input path a couple of frames later, and the focused
    /// control is read again unless the action moved focus or opened another screen (the Guildrun
    /// pattern; a speech hold keeps the order).
    /// </summary>
    public sealed class HelpScreen : Screen
    {
        public const string ActionKey = "mod.help";
        private const int DispatchAfter = 2;
        private const int FocusAfter = 10;

        private static bool s_open;
        private static List<KeyHelpEntry> s_entries = new List<KeyHelpEntry>();

        public static void Toggle()
        {
            if (s_open) { s_open = false; return; }
            s_entries = KeyHelp.Collect(action => action.Key == ActionKey);
            s_open = true;
            Core.Speech.Say(Strings.ScreenHelp, interrupt: true);
        }

        public override string Key => "mod.help";
        public override int Layer => 51;
        public override bool Exclusive => true;
        public override bool IsActive() => s_open;

        public override void Build(GraphBuilder b)
        {
            // No words of its own: opening says "Keys here" itself, interrupting.
            b.PushContext(null, null);
            if (s_entries.Count == 0)
                b.AddItem(ControlId.Structural("help:none"), Nodes.Text(() => Strings.HelpNone));
            foreach (var entry in s_entries)
            {
                var e = entry;
                b.AddItem(ControlId.Structural("help:" + e.ActionKey + ":" + e.Label), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement> { Nodes.LabelPart(() => Strings.HelpRow(e.Label, e.Keys)) },
                    SearchText = () => e.Label,
                    OnActivate = () => Perform(e),
                });
            }
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => s_open = false);
        }

        private static void Perform(KeyHelpEntry entry)
        {
            Core.Speech.BeginHold();
            Navigation.QuietNextLanding();
            s_open = false;
            Later.Frames(DispatchAfter, () => Dispatch(entry), "help dispatch");
        }

        private static void Dispatch(KeyHelpEntry entry)
        {
            var screen = Navigation.BoundScreen;
            var before = Navigation.FocusedNode?.Id;
            if (entry.Run != null)
            {
                try { entry.Run(); }
                catch (Exception e) { Core.CoreLog.Error("[help] running " + entry.Label + " failed: " + e); Core.Speech.EndHold(); return; }
            }
            else if (!InputManager.Dispatch(entry.ActionKey)) { Core.Speech.EndHold(); return; }
            Later.Frames(FocusAfter, () => ReadFocus(screen, before), "help focus");
        }

        private static void ReadFocus(Screen screen, ControlId before)
        {
            try
            {
                if (!Core.Speech.Holding) return;
                var node = Navigation.FocusedNode;
                if (node == null || !ReferenceEquals(Navigation.BoundScreen, screen)) return;
                if (!Equals(node.Id, before)) return;
                string leaf = GraphAnnouncer.LeafText(node);
                if (AlreadySaid(leaf)) return;
                Core.Speech.Say(leaf);
            }
            finally { Core.Speech.EndHold(); }
        }

        private static bool AlreadySaid(string text)
        {
            string clean = Contracts.TextFilter.Clean(text);
            if (string.IsNullOrWhiteSpace(clean)) return false;
            foreach (var said in Core.Speech.Held)
                if (Contracts.TextFilter.Clean(said).IndexOf(clean, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
