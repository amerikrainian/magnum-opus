using System;
using System.IO;

namespace MagnumOpus.Host
{
    /// <summary>
    /// The host's live state once bound: the module loader, the per-frame heartbeat (dev pump, then
    /// the module's tick, read fresh so a hot reload is a field swap), and the reload entry point.
    /// </summary>
    internal static class Runtime
    {
        private static ModHostImpl _host;
        private static Modularity.ModuleLoader _loader;
        // The module tick's last logged failure and how often it has repeated since. Keyed by the
        // exception itself, not cleared by a clean tick: a failure on only some frames (a navigator
        // rebuild every sixth) otherwise alternates with clean ticks and was logged ten times a second.
        private static string _tickError;
        private static int _tickErrorRepeats;
        private static readonly System.Collections.Concurrent.ConcurrentQueue<Action> Posted =
            new System.Collections.Concurrent.ConcurrentQueue<Action>();

        /// <summary>Run an action on the game's main thread at the next frame (from any thread).</summary>
        public static void Post(Action action) => Posted.Enqueue(action);

        public static ModHostImpl Host => _host;
        public static Modularity.ModuleLoader Loader => _loader;

        public static void Start()
        {
            _host = new ModHostImpl(GameBinding.DataDir);
            _loader = new Modularity.ModuleLoader(GameBinding.DataDir, _host);
            if (!_loader.Reload())
                SpeechOutput.Tts.Speak(Core_ModuleFailed, interrupt: true);
#if DEBUG
            try { Dev.DevServer.Instance.Start(); }
            catch (Exception ex) { Log.Error("Dev server failed to start", ex); }
#endif
        }

        // The host has no strings table (that is Core's, reloadable); this one line is English.
        private const string Core_ModuleFailed = "Magnum Opus features failed to load";

        public static void OnGameInitialized()
        {
            if (_host != null) _host.GameInitialized = true;
        }

        public static void TickFrame()
        {
            while (Posted.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Log.Error("[host] posted action failed", ex); }
            }
#if DEBUG
            try { Dev.DevServer.Instance.Pump(); }
            catch (Exception ex) { Log.Error("[host] dev pump failed", ex); }
#endif
            var module = _loader?.Module;
            if (module == null) return;
            try
            {
                module.Tick();
            }
            catch (Exception ex)
            {
                string signature = ex.ToString();
                if (signature == _tickError) { _tickErrorRepeats++; return; }
                if (_tickError != null && _tickErrorRepeats > 0)
                    Log.Error("[module] the previous Tick failure repeated " + _tickErrorRepeats + " more times");
                _tickError = signature;
                _tickErrorRepeats = 0;
                Log.Error("[module] Tick threw; repeats of this failure are counted, not logged, until a reload", ex);
            }
        }

        /// <summary>Swap in the on-disk module. Main thread only. Returns a status line.</summary>
        public static string ReloadModule()
        {
            if (_loader == null) return "[no module loader]\n";
            bool ok = _loader.Reload();
            if (_tickError != null && _tickErrorRepeats > 0)
                Log.Error("[module] the previous Tick failure repeated " + _tickErrorRepeats + " more times");
            _tickError = null;
            _tickErrorRepeats = 0;
#if DEBUG
            if (ok) { try { Dev.DevServer.Instance.ResetEvaluator(); } catch { } }
#endif
            return (ok ? "reloaded: generation " + _loader.Generation : "[reload failed] see the log") + "\n";
        }
    }
}
