using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MagnumOpus.Core;
using MagnumOpus.Module.Game;

namespace MagnumOpus.Module.Patches
{
    /// <summary>
    /// The text the game draws on screens the mod has no model for (the fallback reads it): every
    /// TextureRenderer.RenderTextUnsafe call (under every RenderText: titles, labels, messages, button
    /// captions) while capture is on and the drawing screen has mouse input (the top screen; those
    /// below draw with input off). Kept per frame in draw order with positions, so the fallback can
    /// read top to bottom. Off unless the fallback is up: no cost on modeled screens.
    /// </summary>
    internal static class TextCapture
    {
        public struct Drawn
        {
            public string Text;
            public float X, Y;
        }

        public static bool Enabled;
        private static List<Drawn> _drawing = new List<Drawn>();
        private static object _drawingScreen;
        private static bool _drewWithInput;

        /// <summary>Last frame's drawn text, top of the screen first (the game's y grows upward).</summary>
        public static List<Drawn> LastFrame { get; private set; } = new List<Drawn>();

        /// <summary>The screen on top while last frame was captured, when it drew text with input
        /// (an empty string counts); null otherwise (capture off, a fade running). A screen whose
        /// entry reads captured text waits for this to be itself.</summary>
        public static object LastFrameScreen { get; private set; }

        public static void Apply(Harmony harmony)
        {
            // RenderTextUnsafe, not RenderText: the latter is a three-line wrapper the JIT inlines into its
            // callers, where a patch never runs (the first version captured nothing).
            harmony.Patch(Expr.MethodOf(() => TextureRenderer.RenderTextUnsafe(default(string), default(Vector2), default(StyleableFont), default(Color),
                    default(TextAlignment), 0f, 0f, 0f, 0f, 0, default(Color), default(Texture), 0, false, false)),
                postfix: new HarmonyMethod(typeof(TextCapture), nameof(Postfix)));
            CoreLog.Info("[text] text capture armed");
        }

        public static void NewFrame()
        {
            LastFrame = _drawing.OrderByDescending(d => d.Y).ThenBy(d => d.X).ToList();
            LastFrameScreen = _drewWithInput ? _drawingScreen : null;
            _drawing = new List<Drawn>();
            _drawingScreen = GameScreens.Top;
            _drewWithInput = false;
        }

        // __0 text, __1 position, __14 doRender (by position: shipping parameter names are obfuscated).
        private static void Postfix(string __0, Vector2 __1, bool __14)
        {
            if (!Enabled || !__14 || !InputManager.isMouseButtonInputEnabled) return;
            _drewWithInput = true;
            if (string.IsNullOrWhiteSpace(__0)) return;
            _drawing.Add(new Drawn { Text = __0, X = __1.X, Y = __1.Y });
        }
    }
}
