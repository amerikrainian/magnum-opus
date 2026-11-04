using MagnumOpus.Core.Graph;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>The Steam Workshop upload wait (UploadMessageScreen): its one line. It closes itself;
    /// an error comes back as the game's notice box (MessageBoxGameScreen).</summary>
    public sealed class UploadGameScreen : GameScreen<UploadMessageScreen>
    {
        public override string Key => "game.upload";

        public override void Build(GraphBuilder b)
        {
            b.AddItem(ControlId.Structural("upload:text"), Nodes.Text(() => GameText.Translate("Uploading puzzle to Steam Workshop")));
        }
    }
}
