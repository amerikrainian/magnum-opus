using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using Screen = MagnumOpus.Core.Screens.Screen;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// A mod screen that models a game screen: active while that game screen is on top of the stack
    /// (no fade running), so the poll pushes and pops it with the game's own navigation. Decides which
    /// navigation keys the game still sees while it is focused (<see cref="GameKeys"/>).
    /// </summary>
    public abstract class GameScreen<T> : GameScreen where T : class, IScreen
    {
        protected GameScreen() { Modeled.Add(typeof(T)); }

        /// <summary>The live game screen, or null when it is not on top.</summary>
        protected T Game => GameScreens.TopAs<T>();

        public override bool IsActive() => GameScreens.TopAs<T>() != null && !GameScreens.InTransition;
    }

    public abstract class GameScreen : Screen
    {
        // The game screen types a GameScreen<T> models (the fallback covers the rest). Per module
        // generation: the statics come fresh with each reload.
        protected static readonly System.Collections.Generic.HashSet<System.Type> Modeled = new System.Collections.Generic.HashSet<System.Type>();

        protected static bool IsModeled(System.Type t) => Modeled.Contains(t);

        /// <summary>Whether the game should still see this navigation key (an SDL keycode) while
        /// the screen has focus. Escape by default: the game's own back-out (to the previous screen,
        /// the pause menu) keeps working, and a screen that handles Back itself says false.</summary>
        public virtual bool PassKeyToGame(int sdlKeycode) => sdlKeycode == GameKeys.Escape;
    }
}
