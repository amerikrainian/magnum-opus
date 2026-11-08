using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Contracts;
using MagnumOpus.Core;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.Tutorial;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.Screens;
using MagnumOpus.Module.Screens.Editor;

namespace MagnumOpus.Module.Tutorial
{
    // The game's own InputManager (global namespace) shadows Core's otherwise.
    using InputManager = MagnumOpus.Core.Input.InputManager;

    /// <summary>What a lesson's steps can see, live every time.</summary>
    internal sealed class TutorialContext
    {
        public EditorContext Editor;
        public Func<string, bool> WasPressed;

        public SolutionEditorScreen Ed => Editor.Ed;
        public Solution Solution => Ed.GetSolution();
        public object Stop => Navigation.FocusedStopKey;
        public SimPlayState RunState => Ed.GetSimPlayState();
        public int Cycle => Ed.GetCycle();
        public HexIndex Cursor => Editor.Cursor;

        /// <summary>The molecule viewer is open.</summary>
        public bool Viewing => ScreenManager.Current is MoleculeViewerScreen;

        /// <summary>Whether any of these actions ran since this step became active.</summary>
        public bool Pressed(params string[] actions) => actions.Any(WasPressed);

        /// <summary>The arms the player placed (a lesson's own arms are fixed).</summary>
        public IEnumerable<Part> PlayerArms => Board.Arms(Ed).Where(a => !a.GetIsFixed());

        /// <summary>Whether the board cursor is on a part this test accepts.</summary>
        public bool CursorOn(Func<Part, bool> test) => Solution.parts.Where(test).Any(p => p.GetHexFootprint(Solution).Contains(Cursor));

        /// <summary>The game's highlighted hexes with no part on them.</summary>
        public IEnumerable<HexIndex> FreeHighlights =>
            Solution.GetPuzzle().highlights.Where(h => !Solution.parts.Any(p => p.GetHexFootprint(Solution).Contains(h)));

        /// <summary>The reagent hexes no lesson arm already reaches.</summary>
        public IEnumerable<HexIndex> FreeReagents
        {
            get
            {
                var reached = new HashSet<HexIndex>(Board.Arms(Ed).Where(a => a.GetIsFixed()).SelectMany(a => Board.Grippers(Ed, a).Select(g => g.Key)));
                return Solution.parts.Where(p => p.GetType().isInput).SelectMany(p => p.GetHexFootprint(Solution)).Where(h => !reached.Contains(h));
            }
        }

        /// <summary>A reading of the machine as built: while it runs, parts have moved, so the value
        /// stays what it was when it stopped.</summary>
        public Func<T> WhileStopped<T>(Func<T> read)
        {
            T last = default(T);
            return () =>
            {
                if (RunState == SimPlayState.Stopped) last = read();
                return last;
            };
        }

        /// <summary>One line of one of the puzzle's sticky notes, in the game's words; empty when the
        /// puzzle has no such line.</summary>
        public string Note(int note, int line)
        {
            var notes = Solution.GetPuzzle().notes;
            return note < notes.Length && line < notes[note].points.Length ? GameText.Of(notes[note].points[line]) : "";
        }
    }

    /// <summary>
    /// The mod's lessons (SpeechChem's tutorial engine, in <see cref="TutorialRunner"/>): by puzzle,
    /// ticked from the module while the solution editor is on top, its landing already read. Progress
    /// lives in the settings store. The steps teach the mod's keys, never the solution: see
    /// <see cref="TutorialScripts"/>.
    /// </summary>
    internal static class Tutorials
    {
        private const string StoreKey = "tutorial_done";
        private const int FramesAfterLanding = 3;

        private static ISettingsStore _store;
        private static EditorContext _editor;
        private static Dictionary<string, HashSet<string>> _progress = new Dictionary<string, HashSet<string>>();
        private static TutorialRunner _runner;
        private static string _key, _puzzle;
        private static int _frames;
        private static readonly HashSet<string> _pressed = new HashSet<string>();

        public static void Start(ISettingsStore store)
        {
            _store = store;
            _progress = TutorialProgress.Parse(store.GetString(StoreKey, ""));
            InputManager.ActionHandled = Report;
        }

        /// <summary>The editor screen's shared state, once, when the screen is registered.</summary>
        public static void Bind(EditorContext editor) { _editor = editor; }

        /// <summary>An action or a named event happened (a step's <c>Pressed</c>).</summary>
        public static void Report(string id)
        {
            if (_runner != null) _pressed.Add(id);
        }

        public static bool HasScript(string puzzleId) => TutorialScripts.Has(puzzleId);

        /// <summary>The solution editor's puzzle has a lesson.</summary>
        public static bool Here => _editor?.Ed != null && HasScript(_editor.Ed.GetSolution()?.GetPuzzle().puzzleId);

        public static bool HasActive() => _runner?.Active != null;

        public static string ActiveText() => _runner?.ActiveText;

        /// <summary>The repeat key. With no step it does nothing.</summary>
        public static void Repeat()
        {
            string text = _runner?.ActiveText;
            if (!string.IsNullOrEmpty(text)) Speech.Say(text, interrupt: true);
        }

        /// <summary>Forget this lesson's progress and start it over.</summary>
        public static void Restart()
        {
            if (_puzzle == null) return;
            _progress.Remove(_puzzle);
            Save();
            _key = null;
            _runner = null;
        }

        /// <summary>The key an action is bound to, as the key list says it; read live, so a rebind
        /// shows in every step.</summary>
        public static string Key(string action)
        {
            var bound = InputManager.Find(action);
            return bound == null || bound.Bindings.Count == 0 ? Strings.KeysNone : bound.Bindings[0].DisplayName;
        }

        public static void Tick()
        {
            var game = GameLogic.instance;
            if (game == null || !game.HasScreenOfType<SolutionEditorScreen>())
            {
                // The editor closed: reopening it reads the step again.
                _runner = null;
                _key = null;
                _pressed.Clear();
                return;
            }
            if (GameScreens.TopAs<SolutionEditorScreen>() == null || GameScreens.InTransition || _editor?.Ed == null) return;
            if (!ScreenManager.Stack.Any(s => s is SolutionEditorGameScreen)) return;

            var solution = _editor.Ed.GetSolution();
            if (solution == null) return;
            string puzzle = solution.GetPuzzle().puzzleId;
            string key = puzzle + "\n" + solution.GetFilePath();
            if (key != _key) Load(key, puzzle);
            if (_runner == null || ++_frames < FramesAfterLanding) return;
            _runner.Tick();
        }

        private static void Load(string key, string puzzle)
        {
            _key = key;
            _puzzle = puzzle;
            _frames = 0;
            _pressed.Clear();
            _runner = null;
            if (!HasScript(puzzle)) return;
            try
            {
                var context = new TutorialContext { Editor = _editor, WasPressed = _pressed.Contains };
                _progress.TryGetValue(puzzle, out var done);
                _runner = new TutorialRunner(TutorialScripts.For(puzzle, context), done, id => Latched(puzzle, id),
                    text => Speech.Say(text, interrupt: false), _pressed.Clear);
            }
            catch (Exception e) { CoreLog.Warning("tutorial: loading " + puzzle + " failed: " + e); }
        }

        private static void Latched(string puzzle, string id)
        {
            if (!_progress.TryGetValue(puzzle, out var done)) _progress[puzzle] = done = new HashSet<string>();
            if (done.Add(id)) Save();
        }

        private static void Save()
        {
            try { _store?.SetString(StoreKey, TutorialProgress.Format(_progress)); }
            catch (Exception e) { CoreLog.Warning("tutorial: saving progress failed: " + e.Message); }
        }
    }
}
