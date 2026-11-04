using System;
using System.Runtime.InteropServices;

namespace MagnumOpus.Module.Input
{
    /// <summary>
    /// The mod's own keyboard sensor: a per-frame snapshot of SDL_GetKeyboardState (scancode-indexed,
    /// layout-independent), edge-detected against the previous frame. Taken at the start of the
    /// module's tick (the GameLoop prefix), so it reflects the events the game pumped last frame: the
    /// same latency as the game's own reads. Independent of the game's InputManager, which the
    /// suppression patch blanks for the keys the mod claims. Our own P/Invoke: the game's SDL binding
    /// types its scancodes with an obfuscated enum that has no stable name across builds.
    /// </summary>
    public static class SdlKeyboard
    {
        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetKeyboardState(out int numkeys);

        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_GetKeyFromScancode(int scancode);

        private static byte[] _current = new byte[0];
        private static byte[] _previous = new byte[0];
        private static bool _primed;

        /// <summary>Frames since the module loaded (a monotonic clock for repeats and throttles).</summary>
        public static int Frame { get; private set; }

        public static void Update()
        {
            Frame++;
            var swap = _previous;
            _previous = _current;
            _current = swap;
            IntPtr state = SDL_GetKeyboardState(out int n);
            if (state == IntPtr.Zero || n <= 0) { _primed = false; return; }
            if (_current.Length != n) _current = new byte[n];
            Marshal.Copy(state, _current, 0, n);
            if (_previous.Length != n)
            {
                // The first frame (or SDL resized its table): nothing edges.
                _previous = (byte[])_current.Clone();
            }
            _primed = true;

            // A dev-driver press (dev.key:): held for exactly this frame, released the next, so it
            // edges like a real tap. No OS input and no window focus involved.
            NoteInjection(_injected);
            if (_injected != null)
            {
                foreach (int sc in _injected)
                    if (sc >= 0 && sc < n) _current[sc] = 1;
                _injected = null;
            }
        }

        private static int[] _injected;

        /// <summary>The SDL keycode of the last injected chord's key, for the frame it is held: the
        /// game never sees an injected key (it reads its own SDL events), so the module forwards it
        /// (GameKeys.Forced) where the focused screen passes that key to the game.</summary>
        public static int InjectedGameKey { get; private set; }

        /// <summary>Press a chord on the next frame (the dev driver; tests). Modifiers are scancodes
        /// too (224 LCtrl, 225 LShift, 226 LAlt).</summary>
        public static void Inject(params int[] scancodes) => _injected = scancodes;

        /// <summary>Clear the forwarded key after its frame; set it when an injection lands.</summary>
        internal static void NoteInjection(int[] scancodes)
        {
            InjectedGameKey = 0;
            if (scancodes == null) return;
            foreach (int sc in scancodes)
                if (!IsModifier(sc)) InjectedGameKey = KeyFor(sc);
        }

        public static bool Held(int scancode) => _primed && scancode >= 0 && scancode < _current.Length && _current[scancode] != 0;

        public static bool JustPressed(int scancode) =>
            _primed && scancode >= 0 && scancode < _current.Length && _current[scancode] != 0 && _previous[scancode] == 0;

        public static bool Released(int scancode) =>
            _primed && scancode >= 0 && scancode < _current.Length && _current[scancode] == 0 && _previous[scancode] != 0;

        /// <summary>Any key went down this frame (modifiers excepted).</summary>
        public static bool AnyJustPressed
        {
            get
            {
                if (!_primed) return false;
                for (int i = 0; i < _current.Length; i++)
                    if (_current[i] != 0 && _previous[i] == 0 && !IsModifier(i)) return true;
                return false;
            }
        }

        private const int LCtrl = 224, LShift = 225, LAlt = 226, LGui = 227, RCtrl = 228, RShift = 229, RAlt = 230, RGui = 231;

        public static bool IsModifier(int scancode) => scancode >= LCtrl && scancode <= RGui;

        public static bool CtrlHeld => Held(LCtrl) || Held(RCtrl);
        public static bool ShiftHeld => Held(LShift) || Held(RShift);
        public static bool AltHeld => Held(LAlt) || Held(RAlt);

        /// <summary>The SDL keycode a scancode produces on the current layout (a letter key gives its
        /// lowercase character).</summary>
        public static int KeyFor(int scancode)
        {
            try { return SDL_GetKeyFromScancode(scancode); }
            catch { return 0; }
        }
    }

    /// <summary>SDL scancodes (USB HID usage ids): physical positions, layout-independent.</summary>
    public enum Scancode
    {
        A = 4, B = 5, C = 6, D = 7, E = 8, F = 9, G = 10, H = 11, I = 12, J = 13, K = 14, L = 15,
        M = 16, N = 17, O = 18, P = 19, Q = 20, R = 21, S = 22, T = 23, U = 24, V = 25, W = 26,
        X = 27, Y = 28, Z = 29,
        Num1 = 30, Num2 = 31, Num3 = 32, Num4 = 33, Num5 = 34, Num6 = 35, Num7 = 36, Num8 = 37,
        Num9 = 38, Num0 = 39,
        Return = 40, Escape = 41, Backspace = 42, Tab = 43, Space = 44,
        Minus = 45, Equals = 46, LeftBracket = 47, RightBracket = 48, Backslash = 49,
        Semicolon = 51, Apostrophe = 52, Grave = 53, Comma = 54, Period = 55, Slash = 56,
        F1 = 58, F2 = 59, F3 = 60, F4 = 61, F5 = 62, F6 = 63, F7 = 64, F8 = 65, F9 = 66,
        F10 = 67, F11 = 68, F12 = 69,
        Insert = 73, Home = 74, PageUp = 75, Delete = 76, End = 77, PageDown = 78,
        Right = 79, Left = 80, Down = 81, Up = 82,
        KpEnter = 88,
    }
}
