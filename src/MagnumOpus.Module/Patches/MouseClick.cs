using System;
using HarmonyLib;
using MagnumOpus.Core;
using MagnumOpus.Module.Game;

namespace MagnumOpus.Module.Patches
{
    /// <summary>
    /// A left click the game sees at a screen position for one frame, for game code that has no
    /// button to press: Sigmar's Garden picks a marble when a left click lands within 28 pixels of
    /// it. Prefixes on InputManager.MousePos and IsClickPressed answer the forced position and a
    /// pressed left button, only where the game itself would read them: the screen below the top
    /// is updated with mouse input switched off and keeps seeing none (the rule of
    /// <see cref="GameKeys.Forced"/>). The game then runs its exact click path (selection,
    /// matching, sounds, the win and its statistics).
    /// </summary>
    internal static class MouseClick
    {
        private static Vector2 _pos;
        private static int _frames;

        public static void Apply(Harmony harmony)
        {
            harmony.Patch(Expr.MethodOf(() => global::InputManager.MousePos()),
                prefix: new HarmonyMethod(typeof(MouseClick), nameof(MousePosPrefix)));
            harmony.Patch(Expr.MethodOf(() => global::InputManager.IsClickPressed(default(MouseButtonType))),
                prefix: new HarmonyMethod(typeof(MouseClick), nameof(ClickPrefix)));
            CoreLog.Info("[mouse] forced clicks armed");
        }

        /// <summary>A left click at this screen position (the game's coordinates) during the next
        /// game frame.</summary>
        public static void At(Vector2 pos)
        {
            _pos = pos;
            _frames = 1;
        }

        /// <summary>Called at the start of the module's tick: the frame the click was for is over.</summary>
        public static void NewFrame()
        {
            if (_frames > 0) _frames--;
        }

        private static bool Active => _frames > 0;

        private static bool MousePosPrefix(ref Vector2 __result)
        {
            try
            {
                if (!Active || !global::InputManager.isMouseMovementInputEnabled) return true;
                // A real pointer reads through the input context stack (scroll regions) the same way.
                __result = _pos + global::InputManager.currentContext;
                return false;
            }
            catch (Exception e) { CoreLog.Warning("[mouse] position: " + e.Message); return true; }
        }

        // __0 = the button: shipping parameter names are obfuscated, so patches bind by position.
        private static bool ClickPrefix(object[] __args, ref bool __result)
        {
            try
            {
                if (!Active || Convert.ToInt32(__args[0]) != (int)MouseButtonType.LeftClick) return true;
                __result = global::InputManager.isMouseButtonInputEnabled;
                return false;
            }
            catch (Exception e) { CoreLog.Warning("[mouse] click: " + e.Message); return true; }
        }
    }
}
