using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MagnumOpus.Core;
using MagnumOpus.Module.Game;

namespace MagnumOpus.Module.Patches
{
    /// <summary>
    /// Presses the game's own immediate-mode buttons by label. Every button press in the game is
    /// ButtonDrawingLogic.RenderAndCheckIfPressed (hover + left click); the labeled ones come from
    /// UIUtils.TextButton / FancyTextButton / SolutionButton / PuzzleButton, whose returned struct
    /// carries a getBounds closure unique to the call. A postfix on those factories remembers the
    /// closure's label; a postfix on RenderAndCheckIfPressed answers true, once, for the button whose
    /// label was asked for, when it is enabled and the screen has mouse input (a screen below the top
    /// draws its buttons with input off: the same rule as GameKeys.Forced). The game then runs its
    /// exact click path: its screen pushes, its message boxes with their layout, its sounds.
    /// The labels drawn last frame are kept for screens that list them.
    /// </summary>
    internal static class ButtonPress
    {
        private static readonly ConditionalWeakTable<object, string> Labels = new ConditionalWeakTable<object, string>();
        private static string _pending;
        private static int _pendingFrames;

        private static List<string> _drawing = new List<string>();

        /// <summary>The labeled buttons the game drew last frame with input access, in draw order.</summary>
        public static List<string> LastFrame { get; private set; } = new List<string>();

        public static void Apply(Harmony harmony)
        {
            var remember = new HarmonyMethod(typeof(ButtonPress), nameof(RememberLabel));
            harmony.Patch(Expr.MethodOf(() => UIUtils.TextButton(default(string), default(Vector2))), postfix: remember);
            harmony.Patch(Expr.MethodOf(() => UIUtils.TextButton(default(string), default(Vector2), 0, 0)), postfix: remember);
            harmony.Patch(Expr.MethodOf(() => UIUtils.FancyTextButton(default(string), default(StyleableFont), default(Vector2), 0f)), postfix: remember);
            harmony.Patch(Expr.MethodOf(() => UIUtils.SolutionButton(default(string), default(Maybe<string>), default(Vector2), 0, false)), postfix: remember);
            harmony.Patch(Expr.MethodOf(() => UIUtils.PuzzleButton(default(string), default(Vector2), 0, false, false)), postfix: remember);
            harmony.Patch(Expr.MethodOf(() => default(ButtonDrawingLogic).RenderAndCheckIfPressed(false, false)),
                postfix: new HarmonyMethod(typeof(ButtonPress), nameof(PressIfAsked)));
            CoreLog.Info("[buttons] button capture armed");
        }

        /// <summary>Press the button with this label (as drawn, markup ignored) during the next game
        /// frame or two; false when no such button was drawn last frame.</summary>
        public static bool Press(string label)
        {
            string want = GameText.Clean(label);
            if (!LastFrame.Exists(l => GameText.Clean(l) == want)) return false;
            _pending = want;
            _pendingFrames = 2;
            return true;
        }

        /// <summary>Called at the start of the module's tick (the GameLoop prefix): the game is about
        /// to draw a new frame.</summary>
        public static void NewFrame()
        {
            LastFrame = _drawing;
            _drawing = new List<string>();
            if (_pending != null && --_pendingFrames < 0) _pending = null;
        }

        private static void RememberLabel(ButtonDrawingLogic __result, object[] __args)
        {
            try
            {
                var target = __result.getBounds?.Target;
                if (__args.Length > 0 && __args[0] is string label && target != null)
                {
                    Labels.Remove(target);
                    Labels.Add(target, label);
                }
            }
            catch { }
        }

        // __0 = isEnabled: shipping parameter names are obfuscated, so patches bind by position.
        private static void PressIfAsked(ref ButtonDrawingLogic __instance, bool __0, ref bool __result)
        {
            try
            {
                var target = __instance.getBounds?.Target;
                if (target == null || !Labels.TryGetValue(target, out string label)) return;
                if (!InputManager.isMouseButtonInputEnabled) return;
                _drawing.Add(label);
                if (_pending != null && __0 && GameText.Clean(label) == _pending)
                {
                    __result = true;
                    _pending = null;
                }
            }
            catch { }
        }
    }
}
