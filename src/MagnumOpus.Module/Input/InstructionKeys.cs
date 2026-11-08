using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Input;
using MagnumOpus.Module.Game;

namespace MagnumOpus.Module.Input
{
    // The game's own InputManager (global namespace) shadows Core's otherwise.
    using InputManager = MagnumOpus.Core.Input.InputManager;

    /// <summary>One instruction the keys can place: its action's id and the game's instruction. The
    /// game's type is read when asked (the module registers its keys before the game's init).</summary>
    internal sealed class InstructionSlot
    {
        public string Name { get; }
        private readonly Func<InstructionType> _type;

        public InstructionSlot(string name, Func<InstructionType> type)
        {
            Name = name;
            _type = type;
        }

        public InstructionType Type => _type();
        public string ActionKey => EditorActions.InstructionPrefix + Name;
    }

    /// <summary>The instructions with a key, each its own rebindable action, in the tray's reading
    /// order (the key help's order).</summary>
    internal static class InstructionSlots
    {
        public static readonly InstructionSlot[] All =
        {
            new InstructionSlot("pivot_ccw", () => InstructionTypes.pivotLeft),
            new InstructionSlot("extend", () => InstructionTypes.extend),
            new InstructionSlot("pivot_cw", () => InstructionTypes.pivotRight),
            new InstructionSlot("drop", () => InstructionTypes.drop),
            new InstructionSlot("move_minus", () => InstructionTypes.moveMinus),
            new InstructionSlot("rotate_ccw", () => InstructionTypes.rotateLeft),
            new InstructionSlot("retract", () => InstructionTypes.retract),
            new InstructionSlot("rotate_cw", () => InstructionTypes.rotateRight),
            new InstructionSlot("grab", () => InstructionTypes.grab),
            new InstructionSlot("move_plus", () => InstructionTypes.movePlus),
            new InstructionSlot("period_override", () => InstructionTypes.periodOverride),
            new InstructionSlot("reset", () => InstructionTypes.reset),
            new InstructionSlot("repeat", () => InstructionTypes.repeat),
            new InstructionSlot("halt", () => InstructionTypes.halt),
        };

        public static bool IsInstruction(InputAction action) =>
            action.Key.StartsWith(EditorActions.InstructionPrefix, StringComparison.Ordinal);

        /// <summary>The keys that place the instruction, as the key list says them, with Alt in front
        /// where it takes Alt (from the board); empty when it has none.</summary>
        public static string Keys(InstructionSlot slot, bool alt = false)
        {
            var action = InputManager.Find(slot.ActionKey);
            if (action == null) return "";
            return string.Join(", ", action.Bindings.Select(b => (alt ? "Alt+" : "") + b.DisplayName));
        }

        /// <summary>The keys of this instruction; empty for one without a key of ours.</summary>
        public static string Keys(InstructionType type)
        {
            if (ReferenceEquals(type, null)) return "";
            foreach (var slot in All)
                if (ReferenceEquals(slot.Type, type)) return Keys(slot);
            return "";
        }
    }

    /// <summary>
    /// A key of an instruction. An instruction takes a key without modifiers, and that one key
    /// answers in three forms: alone it types on the tape, Shift inserts there, Alt programs from the
    /// board; rebinding the instruction moves all three. The sections read Shift and Alt themselves.
    /// </summary>
    internal abstract class InstructionBinding : InputBinding
    {
        protected abstract bool KeyHeld();
        protected abstract bool KeyJustPressed();
        protected abstract bool KeyReleased();

        // Ctrl is the clipboard's, Alt+Shift the skips'.
        private static bool ModifiersMatch() => !SdlKeyboard.CtrlHeld && !(SdlKeyboard.ShiftHeld && SdlKeyboard.AltHeld);

        public override bool JustPressed() => ModifiersMatch() && KeyJustPressed();
        public override bool Held() => ModifiersMatch() && KeyHeld();
        public override bool Released() => KeyReleased();

        public override IEnumerable<string> Combos => new[] { DisplayName, "Shift+" + DisplayName, "Alt+" + DisplayName };

        // On the board only Alt + the key is the instruction's.
        public override IEnumerable<string> CombosIn(string scope) =>
            scope == KeyScopes.Board ? new[] { "Alt+" + DisplayName } : Combos;
    }

    /// <summary>The default key of an instruction: the one the game gives it, which follows its
    /// Keyboard Layout option. Matched as the key's character on the player's layout.</summary>
    internal sealed class LayoutInstructionKey : InstructionBinding
    {
        public const string TypeName = "instr-game";

        private static readonly Func<int, bool> Pressed = SdlKeyboard.JustPressed;
        private static readonly Func<int, bool> Down = SdlKeyboard.Held;
        private static readonly Func<int, bool> Up = SdlKeyboard.Released;

        private readonly InstructionSlot _slot;

        public LayoutInstructionKey(InstructionSlot slot) { _slot = slot; }

        private static int HotkeyOf(InstructionSlot slot)
        {
            var type = slot.Type;
            return ReferenceEquals(type, null) ? 0 : (int)type.GetHotkey();
        }

        private bool Any(Func<int, bool> state)
        {
            int hotkey = 0;
            foreach (var key in EditorKeys.InstructionKeys)
            {
                int scancode = (int)key;
                if (!state(scancode)) continue;
                if (hotkey == 0) hotkey = HotkeyOf(_slot);
                if (hotkey == 0 || SdlKeyboard.KeyFor(scancode) != hotkey) continue;
                // Alt+P is the products readout, Alt on the numpad the board's skips.
                if (SdlKeyboard.AltHeld && (key == Scancode.P || (key >= Scancode.Kp1 && key <= Scancode.KpPeriod))) continue;
                return true;
            }
            return false;
        }

        protected override bool KeyJustPressed() => Any(Pressed);
        protected override bool KeyHeld() => Any(Down);
        protected override bool KeyReleased() => Any(Up);

        public override string DisplayName
        {
            get
            {
                int hotkey = HotkeyOf(_slot);
                if (hotkey != 0)
                    foreach (var key in EditorKeys.InstructionKeys)
                        if (SdlKeyboard.KeyFor((int)key) == hotkey) return KeyboardBinding.KeyName(key);
                return Instructions.GameHotkey(_slot.Type);
            }
        }

        public override string Type => TypeName;
        public override string Serialize() => _slot.Name;

        public static LayoutInstructionKey Deserialize(string data)
        {
            foreach (var slot in InstructionSlots.All)
                if (slot.Name == data) return new LayoutInstructionKey(slot);
            return null;
        }
    }

    /// <summary>A key the player chose for an instruction: that physical key, whatever the layout.</summary>
    internal sealed class FixedInstructionKey : InstructionBinding
    {
        public const string TypeName = "instr";

        public Scancode Key { get; }

        public FixedInstructionKey(Scancode key) { Key = key; }

        protected override bool KeyJustPressed() => SdlKeyboard.JustPressed((int)Key);
        protected override bool KeyHeld() => SdlKeyboard.Held((int)Key);
        protected override bool KeyReleased() => SdlKeyboard.Released((int)Key);

        public override string DisplayName => KeyboardBinding.KeyName(Key);

        public override string Type => TypeName;
        public override string Serialize() => Key.ToString();

        public static FixedInstructionKey Deserialize(string data) =>
            Enum.TryParse(data, out Scancode key) && Enum.IsDefined(typeof(Scancode), key) ? new FixedInstructionKey(key) : null;
    }
}
