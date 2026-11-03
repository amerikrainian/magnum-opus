using System;
using System.IO;
using System.Reflection;
using MagnumOpus.Host;

namespace MagnumOpus
{
    /// <summary>
    /// The mod's only entry point. deploy/Lightning.exe.config names this class as the process's
    /// AppDomainManager, so the CLR instantiates it in the default AppDomain BEFORE the game's entry
    /// point or any game static constructor runs, inside the stock exe: the Steam Play button,
    /// shortcuts and controller launches all load the mod, and no game file is modified ("verify
    /// integrity" ignores extra files; deleting the config restores a vanilla launch).
    ///
    /// The game assembly is NOT loaded yet when this runs (GetEntryAssembly() is null), and loading it
    /// ourselves (Assembly.LoadFrom on the exe, the Echopunks pattern) makes the CLR's own load of
    /// Lightning.exe fail with FileLoadException 0x8000FFFF: the process dies before the window opens
    /// (verified 2026-11-03). So the binding waits for the CLR's own load through the AssemblyLoad
    /// event (the SpeechChem pattern), which still fires before Main runs.
    /// </summary>
    public sealed class Bootstrap : AppDomainManager
    {
        internal const string SteamAppId = "558990";

        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            base.InitializeNewDomain(appDomainInfo);
            if (!AppDomain.CurrentDomain.IsDefaultAppDomain()) return;
            try { Boot(); }
            catch (Exception ex)
            {
                // An exception escaping InitializeNewDomain kills the process before the game starts:
                // whatever went wrong, the player must still get their game.
                try { Log.Error("Bootstrap failed; the game continues without the mod.", ex); }
                catch { }
            }
        }

        private static void Boot()
        {
            Log.Init();
            Log.Info("Magnum Opus " + ModHostImpl.Version + " (AppDomainManager inside the stock exe) starting.");

            // BaseDirectory is the game folder. Pin the working directory to it: the engine resolves
            // Content/ and the native dlls relative to cwd, which a console launch elsewhere would break.
            string gameDir = AppDomain.CurrentDomain.BaseDirectory;
            try { Directory.SetCurrentDirectory(gameDir); }
            catch (Exception ex) { Log.Error("Failed to set the working directory to " + gameDir, ex); }
            EnsureSteamAppId(gameDir);

            // Speech first, so a failure further down can still be announced.
            if (!SpeechOutput.Tts.Init())
                Log.Warning("Speech unavailable; continuing so the game still launches.");
            AppDomain.CurrentDomain.ProcessExit += (s, e) => SpeechOutput.Tts.Shutdown();

            AppDomain.CurrentDomain.AssemblyResolve += Modularity.ModuleAssemblies.Resolve;

            Assembly loaded = FindLoaded("Lightning");
            if (loaded != null) GameBinding.OnGameAssembly(loaded);
            else AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs e)
        {
            if (e.LoadedAssembly.IsDynamic || e.LoadedAssembly.GetName().Name != "Lightning") return;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            try { GameBinding.OnGameAssembly(e.LoadedAssembly); }
            catch (Exception ex) { Log.Error("Game binding failed; the game continues without the mod.", ex); }
        }

        private static Assembly FindLoaded(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                if (!a.IsDynamic && a.GetName().Name == name) return a;
            return null;
        }

        /// <summary>Write steam_appid.txt next to the game so Steamworks accepts a launch that did not
        /// come from Steam. Without it, GameInit's SteamAPI.RestartAppIfNecessary(558990) returns true
        /// and exits this process while Steam starts a fresh one (which loads the mod again: a
        /// cosmetic double launch, not a failure).</summary>
        private static void EnsureSteamAppId(string gameDir)
        {
            string path = Path.Combine(gameDir, "steam_appid.txt");
            try
            {
                if (!File.Exists(path) || File.ReadAllText(path).Trim() != SteamAppId)
                {
                    File.WriteAllText(path, SteamAppId);
                    Log.Info("Wrote steam_appid.txt (" + SteamAppId + ").");
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Could not write steam_appid.txt (" + ex.Message + "); a Steam relaunch bounce may occur.");
            }
        }
    }
}
