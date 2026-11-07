using MagnumOpus.Core.Strings;

namespace MagnumOpus.Module.Game
{
    /// <summary>
    /// Instruction names for speech. The game names both directions of a pair alike ("Rotate
    /// Instruction" for clockwise and counter-clockwise, the same for pivot and move), so the mod's
    /// own short words come from the strings table, matched by the game's instruction objects. A type
    /// the mod does not know (a content mod's) reads the game's name.
    /// </summary>
    public static class Instructions
    {
        public static string Label(InstructionType t)
        {
            if (ReferenceEquals(t, null)) return "";
            string key = Key(t);
            return key != null ? Strings.Instruction(key) : GameText.Of(t.name);
        }

        private static string Key(InstructionType t)
        {
            if (t == InstructionTypes.grab) return "grab";
            if (t == InstructionTypes.drop) return "drop";
            if (t == InstructionTypes.rotateRight) return "rotate_cw";
            if (t == InstructionTypes.rotateLeft) return "rotate_ccw";
            if (t == InstructionTypes.extend) return "extend";
            if (t == InstructionTypes.retract) return "retract";
            if (t == InstructionTypes.pivotRight) return "pivot_cw";
            if (t == InstructionTypes.pivotLeft) return "pivot_ccw";
            if (t == InstructionTypes.movePlus) return "move_plus";
            if (t == InstructionTypes.moveMinus) return "move_minus";
            if (t == InstructionTypes.reset) return "reset";
            if (t == InstructionTypes.repeat) return "repeat";
            if (t == InstructionTypes.halt) return "halt";
            if (t == InstructionTypes.periodOverride) return "period_override";
            if (t == InstructionTypes.idle || t == InstructionTypes.placeholderIdle) return "wait";
            return null;
        }

        /// <summary>The instruction's hotkey as the game draws it on its tile, upper case (its
        /// Keyboard Layout option decides the letter); empty when it has none.</summary>
        public static string Hotkey(InstructionType t)
        {
            if (ReferenceEquals(t, null)) return "";
            try { return GameText.Clean(SolutionEditorScreen.GetCharacterFromKey(t.GetHotkey())).ToUpperInvariant(); }
            catch (System.Exception e) { MagnumOpus.Core.CoreLog.Warning("[editor] hotkey letter: " + e.Message); return ""; }
        }

        /// <summary>The game's name and description of an instruction (its tooltip).</summary>
        public static string Details(InstructionType t)
        {
            if (ReferenceEquals(t, null)) return "";
            return GameText.Of(t.name) + ". " + GameText.Of(t.description);
        }
    }
}
