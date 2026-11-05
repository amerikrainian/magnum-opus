using System.Collections.Generic;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Input;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.UI;
using Xunit;

namespace MagnumOpus.Tests
{
    // A stop may name where focus lands on entering it (the editor's tray: two buttons above the
    // products, focus still landing on the first product); once moved within it, Tab returns there.
    public class StopStartTests
    {
        private sealed class TrayScreen : Screen
        {
            public override string Key => "test.tray";
            public override bool IsActive() => true;
            public override void Build(GraphBuilder b)
            {
                b.BeginStop("board");
                b.AddItem(ControlId.Structural("board"), Item("board"));
                b.BeginStop("tray");
                b.AddItem(ControlId.Structural("info"), Item("Show Information"));
                b.AddItem(ControlId.Structural("diagram"), Item("Show Diagram"));
                b.PushContext("Products", "list");
                b.AddItem(ControlId.Structural("product"), Item("Gold"));
                b.PopContext();
                b.SetStopStart(ControlId.Structural("product"));
            }

            private static NodeVtable Item(string label) => new NodeVtable
            {
                Announcements = new[] { new NodeAnnouncement(() => label, kind: AnnouncementKinds.Label) },
            };
        }

        private static InputAction Action(string key) => new InputAction(key, key) { Category = InputCategory.UI };

        [Fact]
        public void EnteringTheStopLandsOnItsStartAndTheButtonsAreAbove()
        {
            Speech.Speak = (t, i) => { };
            NavInput.Current = new FakeNavInput { FrameCount = 1 };
            var nav = new GraphNavigator();
            Navigation.Active = nav;
            try
            {
                nav.Attach(new TrayScreen());
                nav.EnsureFocus();
                Assert.Equal("board", nav.FocusedNodeId.StructuralKey);

                Assert.True(nav.OnInputJustPressed(Action(UiActions.Next)));
                Assert.Equal("product", nav.FocusedNodeId.StructuralKey);
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Up)));
                Assert.Equal("diagram", nav.FocusedNodeId.StructuralKey);

                // The remembered position wins over the start once there is one.
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Next)));
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Next)));
                Assert.Equal("diagram", nav.FocusedNodeId.StructuralKey);
            }
            finally
            {
                Navigation.Active = new GraphNavigator();
            }
        }
    }
}
