using System.Diagnostics;
using System.Text;
using MagnumOpus.Core.UI;

namespace MagnumOpus.Module.Input
{
    /// <summary>
    /// Core's engine inputs (<see cref="INavInput"/>) over the SDL keyboard snapshot. Typed text for
    /// the type-ahead search comes from the snapshot too: the game clears its own typed character
    /// at the end of every frame, before our tick could read it, so a newly pressed letter, digit or
    /// space key is translated through the current layout (SDL_GetKeyFromScancode) instead.
    /// </summary>
    internal sealed class SdlNavInput : INavInput
    {
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private float _last;
        private float _delta;
        private int _deltaFrame = -1;

        public int FrameCount => SdlKeyboard.Frame;
        public float UnscaledTime => (float)Clock.Elapsed.TotalSeconds;

        public float UnscaledDeltaTime
        {
            get
            {
                if (_deltaFrame != SdlKeyboard.Frame)
                {
                    float now = UnscaledTime;
                    _delta = now - _last;
                    _last = now;
                    _deltaFrame = SdlKeyboard.Frame;
                }
                return _delta;
            }
        }

        public bool CtrlHeld => SdlKeyboard.CtrlHeld;
        public bool AltHeld => SdlKeyboard.AltHeld;
        public bool ShiftHeld => SdlKeyboard.ShiftHeld;
        public bool EscapeDown => SdlKeyboard.JustPressed((int)Scancode.Escape);
        public bool UpHeld => SdlKeyboard.Held((int)Scancode.Up);
        public bool DownHeld => SdlKeyboard.Held((int)Scancode.Down);

        private readonly StringBuilder _typed = new StringBuilder(4);

        // The dev driver's typing (dev.type:TEXT), read as typed on the next frame.
        private static string _injected;
        private static int _injectedFrame = int.MinValue / 2;

        public static void InjectTyped(string text)
        {
            _injected = text;
            _injectedFrame = SdlKeyboard.Frame + 1;
        }

        public string TypedText
        {
            get
            {
                _typed.Length = 0;
                if (!SdlKeyboard.CtrlHeld && !SdlKeyboard.AltHeld)
                {
                    for (int sc = (int)Scancode.A; sc <= (int)Scancode.Num0; sc++)
                        if (SdlKeyboard.JustPressed(sc)) Append(SdlKeyboard.KeyFor(sc));
                    if (SdlKeyboard.JustPressed((int)Scancode.Space)) _typed.Append(' ');
                }
                if (_injected != null && SdlKeyboard.Frame == _injectedFrame) _typed.Append(_injected);
                return _typed.Length == 0 ? "" : _typed.ToString();
            }
        }

        private void Append(int keycode)
        {
            // SDL keycodes below 0x80 are the characters themselves; letters come lowercase.
            if (keycode > 32 && keycode < 127) _typed.Append((char)keycode);
        }
    }
}
