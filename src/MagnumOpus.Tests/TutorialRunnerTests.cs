using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Tutorial;
using Xunit;

namespace MagnumOpus.Tests
{
    // The lesson engine: the first undone step is active, live steps come back, latched ones do not,
    // and each change says the old step's After, then the new step's text.
    public class TutorialRunnerTests
    {
        private readonly List<string> _said = new List<string>();
        private readonly List<string> _saved = new List<string>();
        private bool _pressed, _placed, _ran;

        private TutorialRunner Runner(IEnumerable<string> latched = null) => new TutorialRunner(new[]
        {
            new TutorialStep { Id = "press", Latches = true, Met = () => _pressed, Text = () => "press it", After = () => "pressed" },
            new TutorialStep { Id = "place", Met = () => _placed, Text = () => "place it" },
            new TutorialStep { Id = "run", Latches = true, Met = () => _ran, Text = () => "run it", After = () => "done" },
        }, latched, _saved.Add, _said.Add);

        [Fact]
        public void Steps_run_in_order_with_their_after_lines()
        {
            var r = Runner();
            r.Tick();
            Assert.Equal("press", r.Active.Id);
            _pressed = true;
            r.Tick();
            Assert.Equal("place", r.Active.Id);
            _placed = true;
            r.Tick();
            _ran = true;
            r.Tick();
            Assert.Null(r.Active);
            Assert.Equal(new[] { "press it", "pressed", "place it", "run it", "done" }, _said);
            Assert.Equal(new[] { "press", "run" }, _saved);
        }

        [Fact]
        public void A_latched_step_must_be_active_before_it_counts()
        {
            _ran = true;
            var r = Runner();
            r.Tick();
            _pressed = true;
            r.Tick();
            Assert.Equal("place", r.Active.Id);
            Assert.DoesNotContain("run", _saved);
        }

        [Fact]
        public void A_live_step_comes_back_and_a_latched_one_does_not()
        {
            var r = Runner(new[] { "press" });
            _placed = true;
            r.Tick();
            Assert.Equal("run", r.Active.Id);
            _placed = false;
            _said.Clear();
            r.Tick();
            Assert.Equal("place", r.Active.Id);
            Assert.Equal(new[] { "place it" }, _said);
            _placed = true;
            r.Tick();
            Assert.Equal("run", r.Active.Id);
        }

        [Fact]
        public void A_finished_lesson_stays_silent_until_a_part_goes()
        {
            var r = Runner(new[] { "press", "run" });
            _placed = true;
            r.Tick();
            Assert.Null(r.Active);
            Assert.Empty(_said);
            _placed = false;
            r.Tick();
            Assert.Equal("place", r.Active.Id);
        }

        [Fact]
        public void An_after_line_is_said_once_per_visit()
        {
            var r = Runner(new[] { "press", "run" });
            r.Tick();
            _placed = true;
            r.Tick();
            _placed = false;
            r.Tick();
            _placed = true;
            r.Tick();
            Assert.Equal(1, _said.Count(s => s == "done"));
        }

        [Fact]
        public void Progress_round_trips_and_drops_empty_lessons()
        {
            var all = TutorialProgress.Parse("P044:welcome,step;P001:goal");
            Assert.Equal(new HashSet<string> { "welcome", "step" }, all["P044"]);
            all["P001"].Clear();
            Assert.Equal("P044:welcome,step", TutorialProgress.Format(all));
            Assert.Empty(TutorialProgress.Parse(""));
        }
    }
}
