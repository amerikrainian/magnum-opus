using System.Collections.Generic;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Input;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.UI;
using Xunit;

namespace MagnumOpus.Tests
{
    // User rule 2026-11-04: Tab and Shift+Tab wrap round the ends on every screen; with a single stop
    // they do nothing and say nothing.
    public class TabWrapTests
    {
        private sealed class StopsScreen : Screen
        {
            private readonly string[] _stops;
            public StopsScreen(params string[] stops) { _stops = stops; }
            public override string Key => "test.stops";
            public override bool IsActive() => true;
            public override void Build(GraphBuilder b)
            {
                foreach (var stop in _stops)
                {
                    string label = stop;
                    b.BeginStop(stop).AddItem(ControlId.Structural(stop), new NodeVtable
                    {
                        Announcements = new[] { new NodeAnnouncement(() => label, kind: AnnouncementKinds.Label) },
                    });
                }
            }
        }

        private static InputAction Action(string key) => new InputAction(key, key) { Category = InputCategory.UI };

        private static (GraphNavigator nav, List<string> spoken) Start(Screen screen)
        {
            var spoken = new List<string>();
            Speech.Speak = (t, i) => spoken.Add(t);
            NavInput.Current = new FakeNavInput { FrameCount = 1 };
            var nav = new GraphNavigator();
            nav.Attach(screen);
            nav.EnsureFocus();
            return (nav, spoken);
        }

        [Fact]
        public void TabWrapsForwardAndBack()
        {
            try
            {
                var (nav, spoken) = Start(new StopsScreen("Board", "Program", "Log"));
                Assert.Equal("Board", spoken[spoken.Count - 1]);
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Prev)));
                Assert.Equal("Log", spoken[spoken.Count - 1]);
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Next)));
                Assert.Equal("Board", spoken[spoken.Count - 1]);
            }
            finally { Speech.Speak = (t, i) => { }; }
        }

        [Fact]
        public void ALoneStopIgnoresTabSilently()
        {
            try
            {
                var (nav, spoken) = Start(new StopsScreen("Board"));
                int before = spoken.Count;
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Next)));
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Prev)));
                Assert.Equal(before, spoken.Count);
            }
            finally { Speech.Speak = (t, i) => { }; }
        }
    }
}
