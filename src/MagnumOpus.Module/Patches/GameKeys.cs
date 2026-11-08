using System;
using System.Collections.Generic;
using HarmonyLib;
using MagnumOpus.Core;
using MagnumOpus.Core.Screens;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;
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

        /// <summary>Whether the screen on top hands this key to the game: focus mode off, no modeled
        /// screen, or a game screen that passes it. A mod overlay (the key help, the mod menu) and a
        /// child screen (a dropdown's list, a prompt) own every key they get: Escape closing a
        /// dropdown list must not also close the screen beneath it.</summary>
        public static bool PassedToGame(int key)
        {
            if (!FocusMode.Active) return true;
            var screen = ScreenManager.Current;
            if (screen == null) return true;
            if (screen.ParentScreen != null) return false;
            return screen is GameScreen gs && gs.PassKeyToGame(key);
        }

        /// <summary>Whether the game should see this key now: the screen passes it, and it is not an
        /// Escape the player has moved Back away from.</summary>
        public static bool GameSees(int key) =>
            PassedToGame(key) && !(FocusMode.Active && key == Escape && !BackIsEscape());

        private static bool BackIsEscape()
        {
            var back = Core.Input.InputManager.Find(Core.UI.UiActions.Back);
            if (back == null) return true;
            foreach (var b in back.Bindings)
                if (b is KeyboardBinding k && k.Key == Scancode.Escape && !k.Ctrl && !k.Shift && !k.Alt) return true;
            return false;
        }

        private static int _reboundFrame = -1;
        private static readonly HashSet<int> _rebound = new HashSet<int>();

        // A key the player bound to something that answers on this screen: the game must not act on
        // it too. Only changed actions: the defaults are the claimed keys above and ClaimsKey.
        private static bool Rebound(int key)
        {
            if (!FocusMode.Active || Core.Input.KeyBindings.Changed.Count == 0) return false;
            if (_reboundFrame != SdlKeyboard.Frame)
            {
                _reboundFrame = SdlKeyboard.Frame;
                _rebound.Clear();
                foreach (var action in Core.Input.KeyBindings.Changed)
                {
                    if (!Core.Input.InputManager.IsLive(action)) continue;
                    if (action.Category != Core.Input.InputCategory.Global && !Core.UI.Navigation.WouldHandle(action)) continue;
                    foreach (var binding in action.Bindings)
                    {
                        if (binding is KeyboardBinding keyboard) _rebound.Add(SdlKeyboard.KeyFor((int)keyboard.Key));
                        else if (binding is FixedInstructionKey instruction) _rebound.Add(SdlKeyboard.KeyFor((int)instruction.Key));
                    }
                }
            }
            return _rebound.Contains(key);
        }

        // A modeled game screen anywhere on our stack that takes this key too (the editor's letters
        // stay ours while the key help is open over it).
        private static bool ScreenClaims(int key)
        {
            if (!FocusMode.Active) return false;
            foreach (var s in ScreenManager.Stack)
                if (s is GameScreen gs && gs.ClaimsKey(key)) return true;
            return false;
        }

        private static bool Prefix(object[] __args, ref bool __result)
        {
            try
            {
                int key = Convert.ToInt32(__args[0]);
                // A synthesized press reaches only the screen with input access, as a real key does:
                // the game updates the screens below the top one too (with isKeyInputEnabled off),
                // and a forced Escape seen there opened the options menu under a letter, which the
                // letter's own close then popped instead of itself.
                if (Forced != 0 && key == Forced)
                {
                    __result = global::InputManager.isKeyInputEnabled;
                    return false;
                }
                // A key being captured for a rebind is nobody else's.
                if (ScreenManager.Current != null && ScreenManager.Current.CapturesRawInput)
                {
                    __result = false;
                    return false;
                }
                if (!(Claimed.Contains(key) || ScreenClaims(key) || Rebound(key)) || GameSees(key)) return true;
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
