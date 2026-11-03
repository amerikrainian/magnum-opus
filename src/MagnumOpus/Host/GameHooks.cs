using System;
using System.Reflection;
using HarmonyLib;

namespace MagnumOpus.Host
{
    /// <summary>
    /// The two permanent host hooks, the module's heartbeat: a postfix on GameLogic.GameInit (the first
    /// screen exists) and a prefix on GameLogic.GameLoop (once per frame, on the game's main thread).
    /// AppInit.Main is `new GameLogic(); GameInit(); while (true) GameLoop();`. Resolved by human name
    /// through the build's name table and attached once, never unpatched. Nothing ticks until the boot
    /// gate (the loading screen waits for a click inside GameInit) has passed.
    /// </summary>
    internal static class GameHooks
    {
        private static Harmony _harmony;

        public static bool Attach()
        {
            try
            {
                MethodInfo init = GameBinding.Method("GameLogic", "GameInit", 0);
                MethodInfo loop = GameBinding.Method("GameLogic", "GameLoop", 0);
                if (init == null || loop == null)
                {
                    Log.Error("[hooks] GameLogic.GameInit/GameLoop unresolved (init=" + (init != null) + ", loop=" + (loop != null) + ").");
                    return false;
                }
                _harmony = new Harmony("com.magnumopus.host");
                _harmony.Patch(init, postfix: new HarmonyMethod(typeof(GameHooks).GetMethod(nameof(AfterInit), BindingFlags.Static | BindingFlags.NonPublic)));
                _harmony.Patch(loop, prefix: new HarmonyMethod(typeof(GameHooks).GetMethod(nameof(BeforeLoop), BindingFlags.Static | BindingFlags.NonPublic)));
                Log.Info("[hooks] attached: GameInit postfix, GameLoop prefix.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[hooks] attaching failed", ex);
                return false;
            }
        }

        private static void AfterInit()
        {
            try { Runtime.OnGameInitialized(); }
            catch (Exception ex) { Log.Error("[hooks] init postfix threw", ex); }
        }

        private static void BeforeLoop()
        {
            // Runtime.TickFrame swallows and logs everything itself; a throw here would kill the game.
            try { Runtime.TickFrame(); }
            catch { }
        }
    }
}
