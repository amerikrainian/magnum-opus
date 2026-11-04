using MagnumOpus.Core.Input;
using MagnumOpus.Module.Screens;

namespace MagnumOpus.Module.Input
{
    // The game's own InputManager (global namespace) shadows Core's otherwise.
    using InputManager = MagnumOpus.Core.Input.InputManager;
    using S = SolutionEditorGameScreen;

    /// <summary>
    /// The solution editor's keys, in the UI category: the navigator hands each to the focused
    /// screen by its key, and only the editor answers them (anywhere else they do nothing). The hex
    /// cluster is the physical Q E A D Z C block (scancodes: the same keys on AZERTY), mirrored on
    /// the numpad (7 9 4 6 1 3) as Civ V Access does.
    /// </summary>
    internal static class EditorKeys
    {
        public static void Register()
        {
            Hex(S.HexNW, "Cursor northwest", Scancode.Q, Scancode.Kp7);
            Hex(S.HexNE, "Cursor northeast", Scancode.E, Scancode.Kp9);
            Hex(S.HexW, "Cursor west", Scancode.A, Scancode.Kp4);
            Hex(S.HexE, "Cursor east", Scancode.D, Scancode.Kp6);
            Hex(S.HexSW, "Cursor southwest", Scancode.Z, Scancode.Kp1);
            Hex(S.HexSE, "Cursor southeast", Scancode.C, Scancode.Kp3);
            Skip(S.SkipNW, "Skip northwest", Scancode.Q, Scancode.Kp7);
            Skip(S.SkipNE, "Skip northeast", Scancode.E, Scancode.Kp9);
            Skip(S.SkipW, "Skip west", Scancode.A, Scancode.Kp4);
            Skip(S.SkipE, "Skip east", Scancode.D, Scancode.Kp6);
            Skip(S.SkipSW, "Skip southwest", Scancode.Z, Scancode.Kp1);
            Skip(S.SkipSE, "Skip southeast", Scancode.C, Scancode.Kp3);

            Key(S.TapePrev, "Program cursor back a cycle", new KeyboardBinding(Scancode.Semicolon)).Repeating();
            Key(S.TapeNext, "Program cursor forward a cycle", new KeyboardBinding(Scancode.Apostrophe)).Repeating();
            var digits = new[] { Scancode.Num1, Scancode.Num2, Scancode.Num3, Scancode.Num4, Scancode.Num5, Scancode.Num6, Scancode.Num7, Scancode.Num8, Scancode.Num9 };
            for (int i = 0; i < digits.Length; i++)
                Key(S.ArmPrefix + (i + 1), "Jump to arm " + (i + 1), new KeyboardBinding(digits[i]));
            Key(S.CatPrev, "Previous category", new KeyboardBinding(Scancode.LeftBracket));
            Key(S.CatNext, "Next category", new KeyboardBinding(Scancode.RightBracket));
            Key(S.ItemPrev, "Previous item", new KeyboardBinding(Scancode.Comma));
            Key(S.ItemNext, "Next item", new KeyboardBinding(Scancode.Period));
            Key(S.Details, "Details", new KeyboardBinding(Scancode.S));
            Key(S.MoleculeKey, "Molecule", new KeyboardBinding(Scancode.W));
            Key(S.Neighbours, "Neighbours", new KeyboardBinding(Scancode.X));
            Key(S.Score, "Score", new KeyboardBinding(Scancode.S, alt: true));
            Key(S.Tips, "Tutorial tips", new KeyboardBinding(Scancode.T, alt: true));
            Key(S.Play, "Play or pause", new KeyboardBinding(Scancode.Space));
            Key(S.Step, "Step one cycle", new KeyboardBinding(Scancode.Space, ctrl: true)).Repeating();
            Key(S.Fast, "Fast forward", new KeyboardBinding(Scancode.Space, alt: true));
        }

        private static InputAction Key(string key, string label, KeyboardBinding binding) =>
            InputManager.Register(key, label, InputCategory.UI).AddBinding(binding);

        private static void Hex(string key, string label, Scancode letter, Scancode pad) =>
            Key(key, label, new KeyboardBinding(letter)).AddBinding(new KeyboardBinding(pad)).Repeating();

        private static void Skip(string key, string label, Scancode letter, Scancode pad) =>
            Key(key, label, new KeyboardBinding(letter, alt: true)).AddBinding(new KeyboardBinding(pad, alt: true)).Repeating();
    }
}
