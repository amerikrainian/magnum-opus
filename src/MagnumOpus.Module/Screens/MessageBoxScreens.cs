using System.Collections.Generic;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The game's message boxes (MessageBoxScreen): a notice (message, one button), a confirmation
    /// (message, an optional second part, confirm and cancel), or a text box (message and a field the
    /// game types into itself: GetCharByInput for characters, Backspace held to erase). Buttons call
    /// the box's own callbacks the way its widgets do (onConfirm / onCancel, pop, click sound);
    /// Confirm is disabled while a text box is empty, as in the game. In a text box the field is an
    /// edit: the game keeps every typed character and Backspace, the mod's type-ahead stands down,
    /// and each change is echoed. Enter on the field confirms; Escape passes to the game, which
    /// cancels.
    /// </summary>
    public sealed class MessageBoxGameScreen : GameScreen<MessageBoxScreen>
    {
        public override string Key => "game.messagebox";

        /// <summary>A text box is up: the game owns typed characters (the mod binds no bare letters,
        /// and type-ahead stands down through AllowsTypeahead); the navigation keys stay ours.</summary>
        public static bool TextActive => GameScreens.TopAs<MessageBoxScreen>()?.isTextbox == true;

        private string _lastText;

        public override bool PassKeyToGame(int sdlKeycode) =>
            sdlKeycode == GameKeys.Escape || (sdlKeycode == GameKeys.Backspace && TextActive);

        public override bool AllowsTypeahead => !TextActive;

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g == null) return;
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("box:message"), Nodes.Text(() => GameText.Clean(Game?.message)));
            if (g.messagePart2.HasValue())
                b.AddItem(ControlId.Structural("box:message2"), Nodes.Text(() => Game != null && Game.messagePart2.HasValue() ? GameText.Clean(Game.messagePart2.GetValue()) : ""));
            if (g.isTextbox)
            {
                b.AddItem(ControlId.Structural("box:field"), new NodeVtable
                {
                    ControlType = ControlTypes.Edit,
                    Announcements = new List<NodeAnnouncement>
                    {
                        Nodes.LabelPart(() => GameText.Clean(Game?.message)),
                        Nodes.ValuePart(() => string.IsNullOrEmpty(Game?.defaultValue) ? Strings.ValueBlank : Game.defaultValue),
                    },
                    OnActivate = Confirm,
                });
            }
            if (g.confirmable)
                b.AddItem(ControlId.Structural("box:confirm"), Nodes.Button(() => GameText.Clean(Game?.confirmText), Confirm, CanConfirm));
            b.AddItem(ControlId.Structural("box:cancel"), Nodes.Button(() => GameText.Clean(Game?.cancelText), Cancel));
            if (g.isTextbox) b.SetStart(ControlId.Structural("box:field"));
            b.PopContext();
        }

        private bool CanConfirm()
        {
            var g = Game;
            return g != null && (!g.isTextbox || g.defaultValue.Length > 0);
        }

        // The confirm widget's press (Enter is ours, so the game's own IsEnterPressed never fires).
        private void Confirm()
        {
            var g = Game;
            if (g == null || !g.confirmable || !CanConfirm()) return;
            g.onConfirm();
            GameLogic.instance.PopScreen();
            Assets.sounds.click_button.PlaySound(1f);
        }

        private void Cancel()
        {
            var g = Game;
            if (g == null) return;
            g.onCancel();
            GameLogic.instance.PopScreen();
            Assets.sounds.click_button.PlaySound(1f);
        }

        // Echo what the game's field did this frame: a typed character, or what Backspace erased.
        public override void OnUpdate()
        {
            var g = Game;
            if (g == null || !g.isTextbox) { _lastText = null; return; }
            string now = g.defaultValue ?? "";
            if (_lastText == null) { _lastText = now; return; }
            if (now == _lastText) return;
            if (now.Length > _lastText.Length && now.StartsWith(_lastText))
                Speech.Say(Echo(now.Substring(_lastText.Length)), interrupt: true);
            else if (now.Length < _lastText.Length && _lastText.StartsWith(now))
                Speech.Say(Strings.EditDeleted(Echo(_lastText.Substring(now.Length))), interrupt: true);
            else
                Speech.Say(string.IsNullOrEmpty(now) ? Strings.ValueBlank : now, interrupt: true);
            _lastText = now;
        }

        private static string Echo(string s) => s == " " ? Strings.ValueSpace : s;
    }
}
