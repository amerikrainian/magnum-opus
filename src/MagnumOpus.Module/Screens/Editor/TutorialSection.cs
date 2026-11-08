using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Tutorial;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens.Editor
{
    /// <summary>The Tutorial stop on a puzzle with a lesson: the step being waited for, while there is
    /// one, and Restart tutorial, which forgets this lesson's progress.</summary>
    internal sealed class TutorialSection : ScreenSection
    {
        public const string Stop = "editor.tutorial";

        public override void Build(GraphBuilder b)
        {
            if (!Tutorials.Here) return;
            b.BeginStop(Stop);
            b.PushContext(Strings.TutorialStop, null, positions: false);
            if (Tutorials.HasActive())
                b.AddItem(ControlId.Structural("editor:tutorial:step"), Nodes.Text(() => Tutorials.ActiveText()));
            b.AddItem(ControlId.Structural("editor:tutorial:restart"), Nodes.Button(() => Strings.TutorialRestart, Tutorials.Restart));
            b.PopContext();
        }
    }
}
