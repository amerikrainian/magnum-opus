using System;
using System.Collections.Generic;
using System.Text;
using MagnumOpus.Core.Input;

namespace MagnumOpus.Module.Input
{
    /// <summary>
    /// A key chord on the SDL keyboard snapshot: one scancode plus an exact modifier set (Ctrl+A does
    /// not fire a bare A binding). Immutable; serialized "Key|ctrl,shift,alt" for the input settings.
    /// </summary>
    public sealed class KeyboardBinding : InputBinding
    {
        public Scancode Key { get; }
        public bool Ctrl { get; }
        public bool Shift { get; }
        public bool Alt { get; }

        public KeyboardBinding(Scancode key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        private bool ModifiersMatch() =>
            Ctrl == SdlKeyboard.CtrlHeld && Shift == SdlKeyboard.ShiftHeld && Alt == SdlKeyboard.AltHeld;

        public override bool JustPressed() => ModifiersMatch() && SdlKeyboard.JustPressed((int)Key);
        public override bool Held() => ModifiersMatch() && SdlKeyboard.Held((int)Key);
        public override bool Released() => SdlKeyboard.Released((int)Key);

        /// <summary>A bare letter (no Ctrl, no Alt) is also a type-ahead character.</summary>
        public override bool ConflictsWithTypeahead => !Ctrl && !Alt && Key >= Scancode.A && Key <= Scancode.Z;

        public override string DisplayName
        {
            get
            {
                var sb = new StringBuilder();
                if (Ctrl) sb.Append("Ctrl+");
                if (Shift) sb.Append("Shift+");
                if (Alt) sb.Append("Alt+");
                sb.Append(KeyName(Key));
                return sb.ToString();
            }
        }

        private static string KeyName(Scancode key)
        {
            switch (key)
            {
                case Scancode.Up: return "Up Arrow";
                case Scancode.Down: return "Down Arrow";
                case Scancode.Left: return "Left Arrow";
                case Scancode.Right: return "Right Arrow";
                case Scancode.Return: return "Enter";
                case Scancode.KpEnter: return "Numpad Enter";
                case Scancode.PageUp: return "Page Up";
                case Scancode.PageDown: return "Page Down";
                default:
                    string s = key.ToString();
                    return s.StartsWith("Num", StringComparison.Ordinal) && s.Length == 4 ? s.Substring(3) : s;
            }
        }

        public override string Type => "keyboard";

        public override string Serialize()
        {
            var mods = new List<string>();
            if (Ctrl) mods.Add("ctrl");
            if (Shift) mods.Add("shift");
            if (Alt) mods.Add("alt");
            return Key + "|" + string.Join(",", mods);
        }

        public static KeyboardBinding Deserialize(string data)
        {
            var parts = (data ?? "").Split('|');
            if (!Enum.TryParse(parts[0], out Scancode key)) return null;
            bool ctrl = false, shift = false, alt = false;
            if (parts.Length > 1)
                foreach (var m in parts[1].Split(','))
                {
                    if (m == "ctrl") ctrl = true;
                    else if (m == "shift") shift = true;
                    else if (m == "alt") alt = true;
                }
            return new KeyboardBinding(key, ctrl, shift, alt);
        }
    }
}
