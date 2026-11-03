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
        private static bool _tickErrorLogged;

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
#if DEBUG
            try { Dev.DevServer.Instance.Pump(); }
            catch (Exception ex) { Log.Error("[host] dev pump failed", ex); }
#endif
            var module = _loader?.Module;
            if (module == null) return;
            try
            {
                module.Tick();
                _tickErrorLogged = false;
            }
            catch (Exception ex)
            {
                if (_tickErrorLogged) return;
                _tickErrorLogged = true;
                Log.Error("[module] Tick threw; suppressing repeats until it recovers or reloads", ex);
            }
        }

        /// <summary>Swap in the on-disk module. Main thread only. Returns a status line.</summary>
        public static string ReloadModule()
        {
            if (_loader == null) return "[no module loader]\n";
            bool ok = _loader.Reload();
            _tickErrorLogged = false;
#if DEBUG
            if (ok) { try { Dev.DevServer.Instance.ResetEvaluator(); } catch { } }
#endif
            return (ok ? "reloaded: generation " + _loader.Generation : "[reload failed] see the log") + "\n";
        }
    }
}
