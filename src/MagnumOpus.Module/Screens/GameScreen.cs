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

    /// <summary>The base of every game-screen model: a composite (<see cref="CompositeScreen"/>), so a
    /// large screen can be built of sections; a screen without sections overrides Build and friends
    /// itself and behaves as a plain Screen.</summary>
    public abstract class GameScreen : MagnumOpus.Core.Screens.CompositeScreen
    {
        // The game screen types a GameScreen<T> models (the fallback covers the rest). Per module
        // generation: the statics come fresh with each reload.
        protected static readonly System.Collections.Generic.HashSet<System.Type> Modeled = new System.Collections.Generic.HashSet<System.Type>();

        protected static bool IsModeled(System.Type t) => Modeled.Contains(t);

        // Modeled game screens that read the text the game draws (TextCapture), as the fallback does.
        protected static readonly System.Collections.Generic.HashSet<System.Type> ReadsText = new System.Collections.Generic.HashSet<System.Type>();

        /// <summary>Whether the game should still see this navigation key (an SDL keycode) while
        /// the screen has focus. Escape by default: the game's own back-out (to the previous screen,
        /// the pause menu) keeps working, and a screen that handles Back itself says false.</summary>
        public virtual bool PassKeyToGame(int sdlKeycode) => sdlKeycode == GameKeys.Escape;

        /// <summary>Keys beyond the navigation set that this screen takes from the game while it
        /// is up (an SDL keycode): the solution editor takes the letters, digits and punctuation, which
        /// the game would act on by itself (instruction hotkeys under the mouse, Z deleting the
        /// selection). Default none.</summary>
        public virtual bool ClaimsKey(int sdlKeycode) => false;
    }
}
