using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.Screens.Editor;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The solution editor (SolutionEditorScreen), composed of sections that share one
    /// <see cref="EditorContext"/> (Guildrun's pattern): the Tab stops Board, Program, Parts and Run
    /// log (<see cref="BoardSection"/>, <see cref="ProgramSection"/>, <see cref="TraySection"/>,
    /// <see cref="LogSection"/>), plus the engine and undo keys answered from anywhere
    /// (<see cref="RunSection"/>, <see cref="UndoSection"/>). Everywhere, digits 1-9 jump to an arm.
    /// The letters, digits, punctuation, Delete and the numpad are taken from the game while the
    /// editor is up (<see cref="ClaimsKey"/>): its own instruction hotkeys act on the mouse position
    /// and Z deletes the selection. Escape passes to the game (it stops a run, and a stopped editor
    /// closes on a second press) except while a mode wants it: a carried part, a track being drawn, a
    /// selection on the tape. The first press is spoken (<see cref="RunSection"/>).
    /// </summary>
    public sealed class SolutionEditorGameScreen : GameScreen<SolutionEditorScreen>
    {
        private readonly EditorContext _ctx;
        private readonly RunSection _run;
        private bool _tipsPending;

        public SolutionEditorGameScreen()
        {
            _ctx = Add(new EditorContext(() => Game));
            Add(new BoardSection(_ctx));
            Add(new ProgramSection(_ctx));
            Add(new InstructionTraySection(_ctx));
            Add(new TraySection(_ctx));
            Add(new LogSection());
            _run = Add(new RunSection(_ctx));
            Add(new UndoSection(_ctx));
        }

        public override string Key => "game.editor";

        public override string ScreenName
        {
            get
            {
                var s = Game?.GetSolution();
                return s == null ? null : GameText.Of(s.GetPuzzle().puzzleName);
            }
        }

        public override bool AllowsTypeahead => false;

        public override object InitialFocusStop => EditorContext.StopBoard;

        public override bool ClaimsKey(int k) =>
            (k >= 32 && k <= 126) || k == GameKeys.Delete || (k >= 0x40000054 && k <= 0x40000063);

        public override bool PassKeyToGame(int sdlKeycode) => sdlKeycode == GameKeys.Escape && !_ctx.InMode;

        public override void OnPush()
        {
            _ctx.Reset();
            _run.Reset();
            _tipsPending = true;
        }

        public override void OnFocus()
        {
            base.OnFocus();
            if (!_tipsPending) return;
            _tipsPending = false;
            // After the landing is read (the navigator speaks it this frame).
            Later.Frames(2, () => EditorContext.Say(_run.TipsText(), interrupt: false), "editor tips");
        }
    }
}
