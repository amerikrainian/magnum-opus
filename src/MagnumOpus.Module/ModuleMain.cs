using System;
using MagnumOpus.Contracts;
using MagnumOpus.Core;
using MagnumOpus.Core.Strings;
using MagnumOpus.Host;

namespace MagnumOpus.Module
{
    /// <summary>
    /// The module's composition root: wires Core's seams to the host, then runs the per-frame work.
    /// Game patches arm on the FIRST TICK, never in Load: patching a game method runs its type's
    /// static constructor, and Load runs before the game's own init (the Echopunks cold-boot lesson).
    /// </summary>
    public sealed class ModuleMain : IModModule
    {
        private IModHost _host;
        private ModHostImpl _impl;
        private string _lastScreen;

        public void Load(IModHost host)
        {
            _host = host;
            _impl = host as ModHostImpl;
            CoreLog.Info = host.LogInfo;
            CoreLog.Warning = host.LogWarning;
            CoreLog.Error = host.LogError;
            Speech.Speak = (text, interrupt) => host.Speech.Speak(text, interrupt);
            host.LogInfo("[module] loaded, generation " + (_impl?.Generation ?? 0));
        }

        public void Tick()
        {
            if (!_host.LaunchAnnounced)
            {
                _host.LaunchAnnounced = true;
                Speech.Say(Strings.ModLoaded(_host.ModVersion));
                // Matched names announce their own count (with the notice); with a community mapping
                // an unresolved reference is a mod bug, said all the same.
                if (_impl != null && _impl.NameSource != "matched" && _impl.Unresolved.Count > 0)
                    Speech.Say(Strings.Unmatched(_impl.Unresolved.Count));
            }
            SpeakHostNotices();

            // Typed access through the remapped human names: the proof the pipeline binds.
            IScreen top = GameLogic.instance.GetCurrentScreen();
            string name = top == null ? "none" : GameBinding.HumanName(top.GetType());
            if (name != _lastScreen)
            {
                _host.LogInfo("[module] screen: " + name);
                _lastScreen = name;
            }
        }

        // The host's notices (name sources, the mapping download) in the player's language, after the
        // launch line; the unresolved count follows whenever the names change.
        private void SpeakHostNotices()
        {
            while (ModHostImpl.Notices.TryDequeue(out var notice))
            {
                switch (notice)
                {
                    case HostNotice.NamesMatched: Speech.Say(Strings.NamesMatched); break;
                    case HostNotice.MappingDownloading: Speech.Say(Strings.MappingDownloading); break;
                    case HostNotice.MappingNotPublished: Speech.Say(Strings.MappingNotPublished); break;
                    case HostNotice.MappingDownloadFailed: Speech.Say(Strings.MappingFailed); break;
                    case HostNotice.MappingUpdated: Speech.Say(Strings.MappingUpdated); break;
                }
                if ((notice == HostNotice.NamesMatched || notice == HostNotice.MappingUpdated) && _impl != null && _impl.Unresolved.Count > 0)
                    Speech.Say(Strings.Unmatched(_impl.Unresolved.Count));
            }
        }

        public void Dispose()
        {
        }
    }
}
