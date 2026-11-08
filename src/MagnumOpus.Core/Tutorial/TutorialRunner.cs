using System;
using System.Collections.Generic;
using System.Linq;

namespace MagnumOpus.Core.Tutorial
{
    /// <summary>
    /// One tutorial step. Two kinds (SpeechChem's design): LIVE (<see cref="Latches"/> false) is done
    /// while <see cref="Met"/> holds, so removing a part or an undo brings it back; LATCHED is done the
    /// first time <see cref="Met"/> holds while it is the active step, and stays done (saved): keys
    /// pressed, things looked at, a run.
    /// </summary>
    public sealed class TutorialStep
    {
        /// <summary>Stable: the saved progress names it. No ':', ';' or ','.</summary>
        public string Id;

        /// <summary>Spoken when the step becomes active, and by the repeat key.</summary>
        public Func<string> Text;

        /// <summary>Spoken once when the step is met going forward, before the next step. Optional.</summary>
        public Func<string> After;

        public Func<bool> Met;

        public bool Latches;
    }

    /// <summary>
    /// Runs one lesson's steps: the active step is the first not done. When it changes, the old step's
    /// <see cref="TutorialStep.After"/> (going forward, once per visit) then the new step's text are
    /// said. There is no finished state: a finished lesson stays loaded, silent while every live step
    /// holds, and removing a part brings its step back. Nothing is blocked: a step only waits.
    /// </summary>
    public sealed class TutorialRunner
    {
        private readonly List<TutorialStep> _steps;
        private readonly HashSet<string> _latched;
        private readonly Action<string> _onLatched;
        private readonly Action<string> _say;
        private readonly Action _onStepChanged;
        private readonly HashSet<string> _afterSaid = new HashSet<string>();

        public TutorialRunner(IEnumerable<TutorialStep> steps, IEnumerable<string> latched, Action<string> onLatched, Action<string> say, Action onStepChanged = null)
        {
            _steps = steps.ToList();
            _latched = new HashSet<string>(latched ?? new string[0]);
            _onLatched = onLatched;
            _say = say;
            _onStepChanged = onStepChanged;
        }

        /// <summary>The step being waited for; null when every step is done.</summary>
        public TutorialStep Active { get; private set; }

        /// <summary>The active step's text, or null.</summary>
        public string ActiveText => Active == null ? null : Read(Active.Text);

        public void Tick()
        {
            if (Active != null && Active.Latches && !_latched.Contains(Active.Id) && Met(Active))
            {
                _latched.Add(Active.Id);
                _onLatched?.Invoke(Active.Id);
            }
            var next = FirstUndone();
            if (ReferenceEquals(next, Active)) return;

            bool forward = Active != null && Done(Active) && (next == null || _steps.IndexOf(next) > _steps.IndexOf(Active));
            if (forward) SayAfter(Active);
            Active = next;
            _onStepChanged?.Invoke();
            if (next != null) Say(next.Text);
            // Finished from an earlier step: the last step's After is the lesson's closing word.
            else if (forward) SayAfter(_steps[_steps.Count - 1]);
        }

        private void SayAfter(TutorialStep step)
        {
            if (step.After != null && _afterSaid.Add(step.Id)) Say(step.After);
        }

        private void Say(Func<string> text)
        {
            string line = Read(text);
            if (!string.IsNullOrEmpty(line)) _say?.Invoke(line);
        }

        private static string Read(Func<string> text)
        {
            try { return text?.Invoke(); }
            catch (Exception e) { CoreLog.Warning("tutorial: a step's text failed: " + e.Message); return null; }
        }

        private static bool Met(TutorialStep step)
        {
            try { return step.Met(); }
            catch (Exception e) { CoreLog.Warning("tutorial: step " + step.Id + " failed: " + e.Message); return false; }
        }

        private bool Done(TutorialStep step) => step.Latches ? _latched.Contains(step.Id) : Met(step);

        private TutorialStep FirstUndone()
        {
            foreach (var step in _steps)
                if (!Done(step)) return step;
            return null;
        }
    }

    /// <summary>The saved latched steps, per lesson: <c>P044:welcome,step;P001:goal</c>.</summary>
    public static class TutorialProgress
    {
        public static Dictionary<string, HashSet<string>> Parse(string saved)
        {
            var all = new Dictionary<string, HashSet<string>>();
            if (string.IsNullOrWhiteSpace(saved)) return all;
            foreach (var entry in saved.Split(';'))
            {
                int colon = entry.IndexOf(':');
                if (colon <= 0) continue;
                var steps = entry.Substring(colon + 1).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                all[entry.Substring(0, colon).Trim()] = new HashSet<string>(steps.Select(s => s.Trim()));
            }
            return all;
        }

        public static string Format(Dictionary<string, HashSet<string>> all) =>
            string.Join(";", all.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key + ":" + string.Join(",", kv.Value)));
    }
}
