#if DEBUG
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using MagnumOpus.Contracts;
using MagnumOpus.Host;

namespace MagnumOpus.Dev
{
    /// <summary>
    /// Dev-only loopback HTTP driver (DEBUG builds; on by default, MAGNUMOPUS_NO_DEV=1 disables,
    /// MAGNUMOPUS_DEV_PORT sets the port, default 8776). Routes:
    ///   GET  /health                 liveness
    ///   POST /say                    speak the body through the real speech path
    ///   GET  /speech?since=N         what the mod spoke since cursor N (we cannot hear the TTS)
    ///   GET  /log?since=N&amp;grep=S     log lines since cursor N, optionally filtered
    ///   GET  /screen                 the screen stack, bottom to top, by human type name
    ///   GET  /gui                    reflection dump of the active screen with human names
    ///   GET  /screenshot             capture the game window to %LOCALAPPDATA%\MagnumOpus\screenshots
    ///   POST /eval                   C# against the live game, main thread, persistent REPL (see N)
    ///   POST /input                  dispatch an action key (ui.down, ui.activate, ...) as a key press
    ///   GET  /nav                    the module navigator's state
    ///   GET  /actions                every registered action key
    ///   POST /reload                 hot-reload Core + Module from disk
    ///   GET  /module                 generation, dll times, name source, unresolved references
    /// Main-thread routes queue a job pumped from the GameLoop prefix; nothing ticks until the boot
    /// gate has passed, so those time out before it.
    /// </summary>
    internal sealed class DevServer
    {
        public static readonly DevServer Instance = new DevServer();

        public const string DisableEnv = "MAGNUMOPUS_NO_DEV";
        public const string PortEnv = "MAGNUMOPUS_DEV_PORT";
        private const int DefaultPort = 8776;

        private sealed class Job
        {
            public Func<string> Work;
            public string Result = "";
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private readonly SpeechLog _speech = new SpeechLog();
        private readonly CSharpEvaluator _evaluator = new CSharpEvaluator();
        private readonly ConcurrentQueue<Job> _jobs = new ConcurrentQueue<Job>();
        private DevHttpServer _http;
        private bool _enabled;

        public void Start()
        {
            if (_enabled || Environment.GetEnvironmentVariable(DisableEnv) == "1") return;
            int port = DefaultPort;
            string p = Environment.GetEnvironmentVariable(PortEnv);
            if (!string.IsNullOrEmpty(p)) int.TryParse(p, out port);

            MagnumOpus.SpeechOutput.Tts.Observer = _speech.Add;
            try
            {
                _http = new DevHttpServer(port, HandleRequest);
                _http.Start();
                _enabled = true;
                Log.Info("Dev server on http://127.0.0.1:" + port);
            }
            catch (Exception e)
            {
                Log.Error("Dev server failed to start", e);
            }
        }

        public void ResetEvaluator() => _evaluator.Reset();

        public void Pump()
        {
            if (!_enabled) return;
            while (_jobs.TryDequeue(out Job job))
            {
                try { job.Result = job.Work() ?? ""; }
                catch (Exception e) { job.Result = "[host error] " + e + "\n"; }
                job.Done.Set();
            }
        }

        private string OnMainThread(Func<string> work, int timeoutSeconds = 30)
        {
            var job = new Job { Work = work };
            _jobs.Enqueue(job);
            if (!job.Done.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
                return "[timeout] main thread did not run the job within " + timeoutSeconds + "s (boot gate not passed? frozen?)\n";
            return job.Result;
        }

        private static string QueryValue(string query, string key)
        {
            foreach (string kv in query.Split('&'))
                if (kv.StartsWith(key + "=", StringComparison.Ordinal))
                    return Uri.UnescapeDataString(kv.Substring(key.Length + 1));
            return null;
        }

        private static IDevDriver Driver => Runtime.Loader?.Module as IDevDriver;

        // Runs on the HTTP thread.
        private string HandleRequest(string method, string path, string body)
        {
            string route = path, query = "";
            int q = path.IndexOf('?');
            if (q >= 0) { route = path.Substring(0, q); query = path.Substring(q + 1); }

            switch (route)
            {
                case "/":
                case "/health":
                    return "ok\n";
                case "/say" when method == "POST":
                {
                    string text = (body ?? "").Trim();
                    if (text.Length == 0) return "[empty] POST the text to speak as the body\n";
                    MagnumOpus.SpeechOutput.Tts.Speak(text, interrupt: true);
                    return "said: " + text + "\n";
                }
                case "/speech":
                {
                    long.TryParse(QueryValue(query, "since") ?? "0", out long since);
                    string lines = _speech.Render(since, out long next);
                    return "cursor: " + next + "\n" + lines;
                }
                case "/log":
                {
                    long.TryParse(QueryValue(query, "since") ?? "0", out long since);
                    string lines = Log.Since(since, QueryValue(query, "grep"), out long next);
                    return "cursor: " + next + "\n" + lines;
                }
                case "/screen":
                    return OnMainThread(DumpScreens);
                case "/gui":
                    return OnMainThread(ObjectDumper.DumpActiveScreen);
                case "/screenshot":
                    return Screenshot.Capture();
                case "/eval" when method == "POST":
                    if (string.IsNullOrWhiteSpace(body)) return "[empty] POST C# source as the request body\n";
                    return OnMainThread(() => _evaluator.Eval(body));
                case "/input" when method == "POST":
                {
                    string key = (body ?? "").Trim();
                    return OnMainThread(() =>
                    {
                        var d = Driver;
                        if (d == null) return "[no module driver]\n";
                        return (d.DispatchAction(key) ?? "[unknown action] " + key + "; GET /actions lists them") + "\n";
                    });
                }
                case "/nav":
                    return OnMainThread(() => (Driver?.DescribeNav() ?? "[no module driver]") + "\n");
                case "/actions":
                    return OnMainThread(() => (Driver?.ListActions() ?? "[no module driver]") + "\n");
                case "/reload" when method == "POST":
                    return OnMainThread(Runtime.ReloadModule);
                case "/module":
                    return DescribeModule();
                default:
                    return "[404] " + method + " " + route + "\n";
            }
        }

        private static string DumpScreens()
        {
            if (!GameBinding.Bound) return "[not bound]\n";
            var stack = N.Screens();
            if (stack.Count == 0) return "(no screens on the stack yet)\n";
            var sb = new StringBuilder();
            sb.Append("active: ").Append(N.Name(stack[stack.Count - 1])).Append('\n');
            sb.Append("stack (bottom -> top):\n");
            for (int i = 0; i < stack.Count; i++) sb.Append("  ").Append(i).Append(": ").Append(N.Name(stack[i])).Append('\n');
            return sb.ToString();
        }

        private static string DescribeModule()
        {
            var sb = new StringBuilder();
            var loader = Runtime.Loader;
            var host = Runtime.Host;
            sb.Append("build: ").Append(GameBinding.Mvid).Append(" (names: ").Append(GameBinding.Source ?? "none").Append(")\n");
            if (loader == null) return sb.Append("no module loader\n").ToString();
            sb.Append("generation: ").Append(loader.Generation).Append('\n');
            sb.Append("core dll: ").Append(File.GetLastWriteTime(loader.CorePath).ToString("HH:mm:ss")).Append('\n');
            sb.Append("module dll: ").Append(File.GetLastWriteTime(loader.ModulePath).ToString("HH:mm:ss")).Append('\n');
            sb.Append("game initialized: ").Append(host?.GameInitialized).Append('\n');
            var unresolved = host?.Unresolved;
            sb.Append("unresolved: ").Append(unresolved?.Count ?? 0).Append('\n');
            if (unresolved != null) foreach (var u in unresolved) sb.Append("  ").Append(u).Append('\n');
            return sb.ToString();
        }
    }
}
#endif
