using System;
using System.Collections.Generic;
using HarmonyLib;
using MagnumOpus.Core;
using MagnumOpus.Core.Screens;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Screens;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Patches
{
    /// <summary>
    /// Keeps the game from acting on the keys the mod's navigation uses. Every game key read goes
    /// through the static InputManager (IsKeyPressed, IsKeyReleased, IsKeyHeld, WhileKeyHeld; nothing
    /// else reads the window's key sets), so a prefix there that answers "not pressed" blanks a key
    /// for the whole game. Blanked only while focus mode is on and a modeled screen is up that does
    /// not pass the key on (<see cref="GameScreen.PassKeyToGame"/>: Escape passes by default, so the
    /// game's own back-out keeps working). The key arrives as an SDL keycode; read through __args so
    /// this never names the game's key enum.
    /// </summary>
    internal static class GameKeys
    {
        // SDL keycodes of the navigation vocabulary.
        public const int Return = 13, Escape = 27, Backspace = 8, Tab = 9, Space = 32;
        public const int Right = 0x4000004F, Left = 0x40000050, Down = 0x40000051, Up = 0x40000052;
        public const int Home = 0x4000004A, PageUp = 0x4000004B, End = 0x4000004D, PageDown = 0x4000004E;
        public const int KpEnter = 0x40000058, Delete = 127;

        private static readonly HashSet<int> Claimed = new HashSet<int>
        {
            Return, KpEnter, Escape, Backspace, Tab, Right, Left, Down, Up, Home, End, PageUp, PageDown,
        };

        public static void Apply(Harmony harmony)
        {
            var prefix = new HarmonyMethod(typeof(GameKeys), nameof(Prefix));
            harmony.Patch(Expr.MethodOf(() => global::InputManager.IsKeyPressed(default(SDL2.SDL.SDLKey))), prefix: prefix);
            harmony.Patch(Expr.MethodOf(() => global::InputManager.IsKeyReleased(default(SDL2.SDL.SDLKey))), prefix: prefix);
            harmony.Patch(Expr.MethodOf(() => global::InputManager.IsKeyHeld(default(SDL2.SDL.SDLKey))), prefix: prefix);
            harmony.Patch(Expr.MethodOf(() => global::InputManager.WhileKeyHeld(default(SDL2.SDL.SDLKey))), prefix: prefix);
            CoreLog.Info("[keys] game key suppression armed");
        }

        /// <summary>Set for one frame to make the game see a key as pressed (synthesized presses).</summary>
        public static int Forced;

        private static bool Prefix(object[] __args, ref bool __result)
        {
            try
            {
                int key = Convert.ToInt32(__args[0]);
                if (Forced != 0 && key == Forced) { __result = true; return false; }
                if (!Claimed.Contains(key) || !FocusMode.Active) return true;
                var screen = ScreenManager.Current;
                if (screen == null || screen.CapturesRawInput) return true;
                // A mod overlay (the key help, the mod menu, a choice list) owns every key it shows.
                var top = screen;
                while (top.ParentScreen != null) top = top.ParentScreen;
                if (top is GameScreen gs && gs.PassKeyToGame(key)) return true;
                __result = false;
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
