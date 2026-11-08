using MagnumOpus.Core.Input;

namespace MagnumOpus.Module.Input
{
    // The game's own InputManager (global namespace) shadows Core's otherwise.
    using InputManager = MagnumOpus.Core.Input.InputManager;

    /// <summary>The solution editor's action keys (the sections answer them by these ids).</summary>
    internal static class EditorActions
    {
        public const string HexNW = "ed.hex.nw", HexNE = "ed.hex.ne", HexW = "ed.hex.w", HexE = "ed.hex.e", HexSW = "ed.hex.sw", HexSE = "ed.hex.se";
        public const string SkipNW = "ed.skip.nw", SkipNE = "ed.skip.ne", SkipW = "ed.skip.w", SkipE = "ed.skip.e", SkipSW = "ed.skip.sw", SkipSE = "ed.skip.se";
        public const string Origin = "ed.origin";
        public const string ChamberPrev = "ed.chamber.prev", ChamberNext = "ed.chamber.next";
        public const string ArmPrefix = "ed.arm.";
        public const string CatPrev = "ed.cat.prev", CatNext = "ed.cat.next", ItemPrev = "ed.item.prev", ItemNext = "ed.item.next";
        // S reads details on the board only (on the tape S is an instruction); / everywhere.
        public const string Details = "ed.details", Molecule = "ed.molecule", Neighbours = "ed.neighbours";
        public const string Score = "ed.score", Tips = "ed.tips", Products = "ed.products", Diagram = "ed.diagram";
        public const string Play = "ed.play", Step = "ed.step", Fast = "ed.fast";
        // Editing.
        public const string InstructionPrefix = "ed.instr.";
        public const string Delete = "ed.delete";
        public const string Clone = "ed.clone", DrawTrack = "ed.track";
        public const string RotateCcw = "ed.rot.ccw", RotateCw = "ed.rot.cw", Longer = "ed.len.up", Shorter = "ed.len.down";
        public const string SelectUp = "ed.sel.up", SelectDown = "ed.sel.down", SelectLeft = "ed.sel.left", SelectRight = "ed.sel.right", SelectAll = "ed.sel.all";
        public const string Copy = "ed.copy", Cut = "ed.cut", Paste = "ed.paste";
        public const string MarkCorner = "ed.mark", ToggleSelect = "ed.toggle_select", SelectParts = "ed.select_parts";
        public const string Jump = "ed.jump";
        public const string ShiftLeft = "ed.shift.left", ShiftRight = "ed.shift.right";
        public const string PartPrev = "ed.part.prev", PartNext = "ed.part.next";
        public const string Undo = "ed.undo", Redo = "ed.redo";
    }

    /// <summary>Where the editor's keys work, for a rebind's conflicts (<see cref="InputAction.Scopes"/>):
    /// the same key may mean one thing on the board and another on the tape.</summary>
    internal static class KeyScopes
    {
        public const string Board = "board", Tape = "tape", Tray = "tray", Solitaire = "solitaire", Molecule = "molecule", Cinematic = "cinematic";
    }

    /// <summary>The key list's groups.</summary>
    internal static class KeyGroups
    {
        public const string Navigation = "navigation", Board = "board", Program = "program", Instructions = "instructions", Editor = "editor", Global = "global";
    }

    /// <summary>
    /// The solution editor's keys, in the UI category: the navigator hands each to the focused
    /// screen by its key, and only the editor answers them (anywhere else they do nothing). The hex
    /// cluster is the physical Q E A D Z C block (scancodes: the same keys on AZERTY), mirrored on
    /// the numpad (7 9 4 6 1 3) as Civ V Access does. The instruction keys are the game's own: the
    /// key's character on the player's layout is matched against the game's instruction hotkeys
    /// (which follow its Keyboard Layout option).
    /// </summary>
    internal static class EditorKeys
    {
        /// <summary>The keys any of the game's layouts uses for an instruction (QWERTY, AZERTY,
        /// Dvorak, Colemak, QWERTZ letters and punctuation; the numpad layout's keys).</summary>
        public static readonly Scancode[] InstructionKeys =
        {
            Scancode.A, Scancode.B, Scancode.C, Scancode.D, Scancode.E, Scancode.F, Scancode.G, Scancode.H,
            Scancode.I, Scancode.J, Scancode.K, Scancode.L, Scancode.M, Scancode.N, Scancode.O, Scancode.P,
            Scancode.Q, Scancode.R, Scancode.S, Scancode.T, Scancode.U, Scancode.V, Scancode.W, Scancode.X,
            Scancode.Y, Scancode.Z, Scancode.Apostrophe, Scancode.Comma, Scancode.Period, Scancode.Semicolon,
            Scancode.Kp1, Scancode.Kp2, Scancode.Kp3, Scancode.Kp4, Scancode.Kp5, Scancode.Kp6, Scancode.Kp7,
            Scancode.Kp8, Scancode.Kp9, Scancode.KpDivide, Scancode.KpMultiply, Scancode.KpMinus, Scancode.KpPlus,
            Scancode.KpPeriod,
        };

        private static readonly string[] Board = { KeyScopes.Board };
        private static readonly string[] BoardOrTape = { KeyScopes.Board, KeyScopes.Tape };
        private static readonly string[] Editor = { KeyScopes.Board, KeyScopes.Tape, KeyScopes.Tray };
        private static readonly string[] Playing = { KeyScopes.Board, KeyScopes.Tape, KeyScopes.Tray, KeyScopes.Cinematic };
        private static readonly string[] Cursors = { KeyScopes.Board, KeyScopes.Solitaire };

        public static void Register()
        {
            var cursorMoves = new[] { KeyScopes.Board, KeyScopes.Solitaire, KeyScopes.Molecule };
            Hex(EditorActions.HexNW, "Cursor northwest", cursorMoves, Scancode.Q, Scancode.Kp7);
            Hex(EditorActions.HexNE, "Cursor northeast", cursorMoves, Scancode.E, Scancode.Kp9);
            Hex(EditorActions.HexW, "Cursor west", cursorMoves, Scancode.A, Scancode.Kp4);
            Hex(EditorActions.HexE, "Cursor east", cursorMoves, Scancode.D, Scancode.Kp6);
            Hex(EditorActions.HexSW, "Cursor southwest", cursorMoves, Scancode.Z, Scancode.Kp1);
            Hex(EditorActions.HexSE, "Cursor southeast", cursorMoves, Scancode.C, Scancode.Kp3);
            Skip(EditorActions.SkipNW, "Skip northwest", Scancode.Q, Scancode.Kp7);
            Skip(EditorActions.SkipNE, "Skip northeast", Scancode.E, Scancode.Kp9);
            Skip(EditorActions.SkipW, "Skip west", Scancode.A, Scancode.Kp4);
            Skip(EditorActions.SkipE, "Skip east", Scancode.D, Scancode.Kp6);
            Skip(EditorActions.SkipSW, "Skip southwest", Scancode.Z, Scancode.Kp1);
            Skip(EditorActions.SkipSE, "Skip southeast", Scancode.C, Scancode.Kp3);

            // Home / End on the board are the program cursor's first and last instruction.
            Key(EditorActions.Origin, "Cursor to the origin", KeyGroups.Board, Cursors, new KeyboardBinding(Scancode.Home, ctrl: true));
            Key(EditorActions.ChamberPrev, "Previous chamber", KeyGroups.Board, Board, new KeyboardBinding(Scancode.PageUp));
            Key(EditorActions.ChamberNext, "Next chamber", KeyGroups.Board, Board, new KeyboardBinding(Scancode.PageDown));
            Key(EditorActions.CatPrev, "Previous category", KeyGroups.Board, Board, new KeyboardBinding(Scancode.LeftBracket));
            Key(EditorActions.CatNext, "Next category", KeyGroups.Board, Board, new KeyboardBinding(Scancode.RightBracket));
            Key(EditorActions.ItemPrev, "Previous item", KeyGroups.Board, Cursors, new KeyboardBinding(Scancode.Comma));
            Key(EditorActions.ItemNext, "Next item", KeyGroups.Board, Cursors, new KeyboardBinding(Scancode.Period));
            Key(EditorActions.Details, "Details", KeyGroups.Board, new[] { KeyScopes.Board, KeyScopes.Tray }, new KeyboardBinding(Scancode.S));
            Key(EditorActions.Molecule, "Molecule", KeyGroups.Board, new[] { KeyScopes.Board, KeyScopes.Tray, KeyScopes.Molecule }, new KeyboardBinding(Scancode.W));
            Key(EditorActions.Neighbours, "Neighbours", KeyGroups.Board, Cursors, new KeyboardBinding(Scancode.X));
            Key(EditorActions.Clone, "Pick up a copy", KeyGroups.Board, Board, new KeyboardBinding(Scancode.Return, ctrl: true));
            Key(EditorActions.DrawTrack, "Extend track", KeyGroups.Board, Board, new KeyboardBinding(Scancode.Return, shift: true));
            Key(EditorActions.RotateCcw, "Rotate counter-clockwise", KeyGroups.Board, Board, new KeyboardBinding(Scancode.A, shift: true));
            Key(EditorActions.RotateCw, "Rotate clockwise", KeyGroups.Board, Board, new KeyboardBinding(Scancode.D, shift: true));
            Key(EditorActions.Longer, "Longer arm", KeyGroups.Board, Board, new KeyboardBinding(Scancode.W, shift: true));
            Key(EditorActions.Shorter, "Shorter arm", KeyGroups.Board, Board, new KeyboardBinding(Scancode.S, shift: true));
            Key(EditorActions.MarkCorner, "Mark a corner", KeyGroups.Board, Board, new KeyboardBinding(Scancode.Space, shift: true));
            Key(EditorActions.ToggleSelect, "Select or unselect the part here", KeyGroups.Board, Board, new KeyboardBinding(Scancode.Space, ctrl: true, shift: true));
            Key(EditorActions.SelectParts, "Select all parts", KeyGroups.Board, Board, new KeyboardBinding(Scancode.A, ctrl: true));
            Key(EditorActions.PartPrev, "Previous part on this hex", KeyGroups.Board, Board, new KeyboardBinding(Scancode.Up, ctrl: true));
            Key(EditorActions.PartNext, "Next part on this hex", KeyGroups.Board, Board, new KeyboardBinding(Scancode.Down, ctrl: true));

            var digits = new[] { Scancode.Num1, Scancode.Num2, Scancode.Num3, Scancode.Num4, Scancode.Num5, Scancode.Num6, Scancode.Num7, Scancode.Num8, Scancode.Num9 };
            for (int i = 0; i < digits.Length; i++)
                Key(EditorActions.ArmPrefix + (i + 1), "Jump to arm " + (i + 1), KeyGroups.Program, BoardOrTape, new KeyboardBinding(digits[i]));
            Key(EditorActions.SelectUp, "Select up", KeyGroups.Program, BoardOrTape, new KeyboardBinding(Scancode.Up, shift: true)).Repeating();
            Key(EditorActions.SelectDown, "Select down", KeyGroups.Program, BoardOrTape, new KeyboardBinding(Scancode.Down, shift: true)).Repeating();
            Key(EditorActions.SelectLeft, "Select left", KeyGroups.Program, BoardOrTape, new KeyboardBinding(Scancode.Left, shift: true)).Repeating();
            Key(EditorActions.SelectRight, "Select right", KeyGroups.Program, BoardOrTape, new KeyboardBinding(Scancode.Right, shift: true)).Repeating();
            Key(EditorActions.ShiftLeft, "Move instructions a cycle left", KeyGroups.Program, BoardOrTape, new KeyboardBinding(Scancode.Left, ctrl: true, shift: true)).Repeating();
            Key(EditorActions.ShiftRight, "Move instructions a cycle right", KeyGroups.Program, BoardOrTape, new KeyboardBinding(Scancode.Right, ctrl: true, shift: true)).Repeating();
            Key(EditorActions.SelectAll, "Select all instructions", KeyGroups.Program, BoardOrTape, new KeyboardBinding(Scancode.A, ctrl: true, shift: true));
            Key(EditorActions.Jump, "Jump between the board and the tape", KeyGroups.Program, BoardOrTape, new KeyboardBinding(Scancode.Backspace, alt: true));

            // One action per instruction, its default the key the game gives it: alone on the tape,
            // with Shift to insert, with Alt from the board (see InstructionBinding).
            foreach (var slot in InstructionSlots.All)
                InputManager.Register(slot.ActionKey, slot.Name, InputCategory.UI).In(KeyScopes.Tape, KeyScopes.Board)
                    .Grouped(KeyGroups.Instructions).AddBinding(new LayoutInstructionKey(slot));

            // Ctrl+Shift: Alt + a letter is an instruction (S retract, T track minus).
            Key(EditorActions.Play, "Play or pause", KeyGroups.Editor, Playing, new KeyboardBinding(Scancode.Space));
            Key(EditorActions.Step, "Step one cycle", KeyGroups.Editor, Editor, new KeyboardBinding(Scancode.Space, ctrl: true)).Repeating();
            Key(EditorActions.Fast, "Fast forward", KeyGroups.Editor, Editor, new KeyboardBinding(Scancode.Space, alt: true));
            Key(EditorActions.Score, "Score", KeyGroups.Editor, Editor, new KeyboardBinding(Scancode.S, ctrl: true, shift: true));
            Key(EditorActions.Tips, "Tutorial tips", KeyGroups.Editor, Editor, new KeyboardBinding(Scancode.T, ctrl: true, shift: true));
            Key(EditorActions.Diagram, "Show diagram", KeyGroups.Editor, Editor, new KeyboardBinding(Scancode.D, ctrl: true, shift: true));
            Key(EditorActions.Products, "Products", KeyGroups.Editor, Editor, new KeyboardBinding(Scancode.P, alt: true));
            Key(EditorActions.Delete, "Delete", KeyGroups.Editor, BoardOrTape, new KeyboardBinding(Scancode.Delete));
            Key(EditorActions.Copy, "Copy", KeyGroups.Editor, BoardOrTape, new KeyboardBinding(Scancode.C, ctrl: true));
            Key(EditorActions.Cut, "Cut", KeyGroups.Editor, BoardOrTape, new KeyboardBinding(Scancode.X, ctrl: true));
            Key(EditorActions.Paste, "Paste", KeyGroups.Editor, BoardOrTape, new KeyboardBinding(Scancode.V, ctrl: true));
            Key(EditorActions.Undo, "Undo", KeyGroups.Editor, Editor, new KeyboardBinding(Scancode.Z, ctrl: true));
            Key(EditorActions.Redo, "Redo", KeyGroups.Editor, Editor, new KeyboardBinding(Scancode.Y, ctrl: true)).AddBinding(new KeyboardBinding(Scancode.Z, ctrl: true, shift: true));
        }

        private static InputAction Key(string key, string label, string group, string[] scopes, KeyboardBinding binding) =>
            InputManager.Register(key, label, InputCategory.UI).In(scopes).Grouped(group).AddBinding(binding);

        private static void Hex(string key, string label, string[] scopes, Scancode letter, Scancode pad) =>
            Key(key, label, KeyGroups.Board, scopes, new KeyboardBinding(letter)).AddBinding(new KeyboardBinding(pad)).Repeating();

        // Alt+Shift on the letters (Alt alone is an instruction); Alt on the numpad (Shift+numpad
        // turns into the navigation keys with NumLock on).
        private static void Skip(string key, string label, Scancode letter, Scancode pad) =>
            Key(key, label, KeyGroups.Board, Cursors, new KeyboardBinding(letter, alt: true, shift: true)).AddBinding(new KeyboardBinding(pad, alt: true)).Repeating();
    }
}
