using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The game's Controls reference (ControlsScreen): its action / keys pairs as lines ("Undo
    /// change to solution: Ctrl + Z"), then Close. The game's own mouse controls; the mod's keys are
    /// F1 (here) and the mod menu's "All keys".
    /// </summary>
    public sealed class ControlsGameScreen : GameScreen<ControlsScreen>
    {
        public override string Key => "game.controls";
        public override string ScreenName => GameText.Translate("Controls");

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g == null) return;
            for (int i = 0; i < g.controls.Length; i++)
            {
                int index = i;
                b.AddItem(ControlId.Structural("controls:" + i), Nodes.Text(() =>
                {
                    var c = Game?.controls;
                    return c == null || index >= c.Length ? "" : Strings.HelpRow(GameText.Of(c[index].Item1), GameText.Of(c[index].Item2()));
                }));
            }
            b.AddItem(ControlId.Structural("controls:close"), Nodes.Button(() => Strings.ModClose, () =>
            {
                GameLogic.instance.PopScreen();
                Assets.sounds.ui_modal_close.PlaySound(1f);
            }));
        }
    }
}
