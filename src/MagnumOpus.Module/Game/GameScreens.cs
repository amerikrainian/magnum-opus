using System;
using System.Collections.Generic;
using MagnumOpus.Host;

namespace MagnumOpus.Module.Game
{
    /// <summary>
    /// The game's screen stack (GameLogic.screenStack: a list, bottom to top; PushScreen / PopScreen
    /// drive it), read live every time: never cached across frames. Runtime type names are SHIPPING
    /// (obfuscated) names; <see cref="Name"/> gives the human one.
    /// </summary>
    public static class GameScreens
    {
        public static GameLogic Logic => GameLogic.instance;

        /// <summary>The top screen, or null before init.</summary>
        public static IScreen Top
        {
            get
            {
                var logic = GameLogic.instance;
                var stack = logic?.screenStack?.stack;
                return stack == null || stack.Count == 0 ? null : stack[stack.Count - 1];
            }
        }

        /// <summary>The stack, bottom to top (a copy).</summary>
        public static List<IScreen> Stack()
        {
            var stack = GameLogic.instance?.screenStack?.stack;
            return stack == null ? new List<IScreen>() : new List<IScreen>(stack);
        }

        /// <summary>The top screen as T, or null.</summary>
        public static T TopAs<T>() where T : class, IScreen => Top as T;

        /// <summary>The topmost screen of type T anywhere on the stack, or null.</summary>
        public static T Find<T>() where T : class, IScreen
        {
            var stack = GameLogic.instance?.screenStack?.stack;
            if (stack == null) return null;
            for (int i = stack.Count - 1; i >= 0; i--)
                if (stack[i] is T t) return t;
            return null;
        }

        /// <summary>True while a screen transition (the fades between screens) is running.</summary>
        public static bool InTransition => GameLogic.instance != null && !GameLogic.instance.GetScreenTransition().IsEmpty();

        /// <summary>The human name of a game object's type (shipping names translated back).</summary>
        public static string Name(object o) => o == null ? "none" : GameBinding.HumanName(o.GetType());
    }
}
