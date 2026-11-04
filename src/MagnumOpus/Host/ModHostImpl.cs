using System.Diagnostics;
using System.Reflection;
using MagnumOpus.Contracts;

namespace MagnumOpus.Host
{
    /// <summary>
    /// What the permanent host lends the module (Contracts' <see cref="IModHost"/>): the logger, the
    /// speech pipeline over the Prism -> SAPI -> clipboard chain, the settings over settings.json, the
    /// data folder. Plus host state a module reads directly (it references this assembly): whether the
    /// game has finished init, and how many game references the last module load left unresolved.
    /// </summary>
    internal sealed class ModHostImpl : IModHost
    {
        public static string Version
        {
            get
            {
                var info = FileVersionInfo.GetVersionInfo(typeof(ModHostImpl).Assembly.Location);
                return info.FileVersion ?? "0.0.0";
            }
        }

        public ModHostImpl(string dataDir)
        {
            PluginDir = dataDir;
            Speech = new SpeechPipeline(new TtsBackend());
            Settings = new ModSettings(new HostSettingsStore());
            Speech.Muted = System.Environment.GetEnvironmentVariable("MAGNUMOPUS_NO_SPEECH") == "1";
        }

        public void LogInfo(string message) => Log.Info(message);
        public void LogWarning(string message) => Log.Warning(message);
        public void LogError(string message) => Log.Error(message);

        public string ModVersion => Version;
        public SpeechPipeline Speech { get; }
        public ModSettings Settings { get; }
        public string PluginDir { get; }
        public bool LaunchAnnounced { get; set; }
        public bool SuccessorLoaded { get; internal set; }

        /// <summary>True once GameLogic.GameInit has returned (the first screen exists).</summary>
        public bool GameInitialized { get; internal set; }

        /// <summary>1 for the boot load, +1 per successful reload.</summary>
        public int Generation { get; internal set; }

        /// <summary>Things the host must tell the player (the name sources, the mapping download),
        /// queued from any thread; the module speaks them, localized, after the launch line.</summary>
        public static readonly System.Collections.Concurrent.ConcurrentQueue<HostNotice> Notices =
            new System.Collections.Concurrent.ConcurrentQueue<HostNotice>();

        /// <summary>Where the running build's names came from: "bundled", "downloaded", "matched".</summary>
        public string NameSource => GameBinding.Source;

        /// <summary>Game references of the loaded module that the running build lacks (human names).</summary>
        public System.Collections.Generic.IList<string> Unresolved { get; internal set; } = new string[0];
    }

    /// <summary>What the host reports to the player through the module's speech.</summary>
    internal enum HostNotice
    {
        /// <summary>The build has no community mapping; names come from the fingerprint matcher.</summary>
        NamesMatched,
        /// <summary>Fetching the community mapping for this build.</summary>
        MappingDownloading,
        /// <summary>The community has not published a mapping for this build yet.</summary>
        MappingNotPublished,
        /// <summary>The fetch failed (network, GitHub); retried at the next launch.</summary>
        MappingDownloadFailed,
        /// <summary>The downloaded mapping is in use; the module reloaded onto it.</summary>
        MappingUpdated,
        /// <summary>The Steam install is not on the community-edition branch (the one the mod and
        /// the community mappings follow).</summary>
        WrongBranch,
    }

    /// <summary>The speech backend: the host's Tts chokepoint (Prism, then SAPI, then the clipboard).</summary>
    internal sealed class TtsBackend : ISpeechBackend
    {
        public bool IsAvailable => MagnumOpus.SpeechOutput.Tts.Ready;
        public void Speak(string text, bool interrupt) => MagnumOpus.SpeechOutput.Tts.Speak(text, interrupt);
        public void Stop() => MagnumOpus.SpeechOutput.Tts.Stop();
    }

    /// <summary>The settings store over %LOCALAPPDATA%\MagnumOpus\settings.json (flat string values).</summary>
    internal sealed class HostSettingsStore : ISettingsStore
    {
        public bool GetBool(string key, bool defaultValue) => HostConfig.GetBool(key, defaultValue);
        public void SetBool(string key, bool value) => HostConfig.SetBool(key, value);

        public int GetInt(string key, int defaultValue) =>
            int.TryParse(HostConfig.Get(key, null), out int v) ? v : defaultValue;
        public void SetInt(string key, int value) => HostConfig.Set(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public string GetString(string key, string defaultValue) => HostConfig.Get(key, defaultValue);
        public void SetString(string key, string value) => HostConfig.Set(key, value);
    }
}
