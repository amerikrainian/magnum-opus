using System;
using System.Collections.Generic;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;

namespace MagnumOpus.Module.UI
{
    /// <summary>
    /// Node factories for the key graph (the Guildrun GameNodes pattern, engine-free here: the game
    /// draws immediate-mode, so every node reads its label and value live through a delegate). Labels
    /// are read at speak time, never cached.
    /// </summary>
    public static class Nodes
    {
        public static NodeAnnouncement LabelPart(Func<string> label) => new NodeAnnouncement(label, kind: AnnouncementKinds.Label);

        public static NodeAnnouncement ValuePart(Func<string> value) => new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value);

        /// <summary>Silent while enabled, "disabled" otherwise; read on landing, not live.</summary>
        public static NodeAnnouncement DisabledPart(Func<bool> enabled) =>
            new NodeAnnouncement(() => enabled == null || enabled() ? null : Strings.StateDisabled, live: false, kind: AnnouncementKinds.Enabled);

        /// <summary>"selected" when selected, silent otherwise; live.</summary>
        public static NodeAnnouncement SelectedPart(Func<bool> selected) =>
            new NodeAnnouncement(() => selected != null && selected() ? Strings.StateSelected : null, live: true, kind: AnnouncementKinds.Selected);

        public static NodeAnnouncement TooltipPart(Func<string> description) => new NodeAnnouncement(description, kind: AnnouncementKinds.Tooltip);

        public static NodeVtable Text(Func<string> text) => new NodeVtable
        {
            ControlType = ControlTypes.Text,
            Announcements = new[] { LabelPart(text) },
            SearchText = text,
        };

        public static NodeVtable Button(Func<string> label, Action activate, Func<bool> enabled = null, Func<string> detail = null)
        {
            var parts = new List<NodeAnnouncement> { LabelPart(label), DisabledPart(enabled) };
            if (detail != null) parts.Add(TooltipPart(detail));
            return new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = parts,
                SearchText = label,
                OnActivate = () =>
                {
                    if (enabled != null && !enabled()) return;
                    activate?.Invoke();
                },
            };
        }

        /// <summary>An on/off setting: Enter flips it and speaks the new state.</summary>
        public static NodeVtable Toggle(Func<string> label, Func<bool> get, Action<bool> set, Func<bool> enabled = null)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Toggle,
                Announcements = new List<NodeAnnouncement>
                {
                    LabelPart(label),
                    ValuePart(() => get() ? Strings.StateOn : Strings.StateOff),
                    DisabledPart(enabled),
                },
                SearchText = label,
                OnActivate = () =>
                {
                    if (enabled != null && !enabled()) return;
                    set(!get());
                },
                StateText = () => get() ? Strings.StateOn : Strings.StateOff,
            };
        }

        /// <summary>
        /// A choice among named options, as a real combo box (never a slider): the node reads its label
        /// and the current option; Enter opens the options as a list (<see cref="ChoiceSubmenuScreen"/>)
        /// landing on the current one; Enter there picks, Escape closes without choosing. The options
        /// are read when the list opens, the current value whenever it is spoken.
        /// </summary>
        public static NodeVtable Dropdown(Func<string> label, Func<string> current, Func<IReadOnlyList<string>> options,
            Func<int> currentIndex, Action<int> select, Func<bool> enabled = null)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.ComboBox,
                Announcements = new List<NodeAnnouncement> { LabelPart(label), ValuePart(current), DisabledPart(enabled) },
                SearchText = label,
                OnActivate = () =>
                {
                    if (enabled != null && !enabled()) return;
                    var names = options();
                    var choices = new List<ChoiceOption>();
                    for (int i = 0; i < names.Count; i++)
                    {
                        int index = i;
                        choices.Add(new ChoiceOption(names[i], () => select(index)));
                    }
                    ChoiceSubmenuScreen.Open(label(), choices, currentIndex());
                },
            };
        }

        /// <summary>A numeric value stepped with Left and Right (a volume, a percentage): the one
        /// place a slider is right. Each step speaks the new value.</summary>
        public static NodeVtable Slider(Func<string> label, Func<string> value, Action<int, bool> adjust, Func<bool> enabled = null)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Slider,
                Announcements = new List<NodeAnnouncement> { LabelPart(label), ValuePart(value), DisabledPart(enabled) },
                SearchText = label,
                OnAdjust = (sign, large) =>
                {
                    if (enabled != null && !enabled()) return;
                    adjust(sign, large);
                },
                StateText = value,
            };
        }
    }
}
