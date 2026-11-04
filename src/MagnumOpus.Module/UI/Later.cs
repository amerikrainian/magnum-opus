using System;
using System.Collections.Generic;
using MagnumOpus.Core;
using MagnumOpus.Module.Input;

namespace MagnumOpus.Module.UI
{
    /// <summary>Run an action a number of frames from now, on the main thread (after the game has
    /// redrawn, say). Pumped by the module's tick.</summary>
    internal static class Later
    {
        private struct Pending { public int Frame; public Action Action; public string Name; }

        private static readonly List<Pending> _pending = new List<Pending>();

        public static void Frames(int frames, Action action, string name) =>
            _pending.Add(new Pending { Frame = SdlKeyboard.Frame + frames, Action = action, Name = name });

        public static void Tick()
        {
            if (_pending.Count == 0) return;
            int now = SdlKeyboard.Frame;
            for (int i = 0; i < _pending.Count; i++)
            {
                var p = _pending[i];
                if (now < p.Frame) continue;
                _pending.RemoveAt(i--);
                try { p.Action(); }
                catch (Exception e) { CoreLog.Warning("Later: " + p.Name + " failed: " + e.Message); }
            }
        }
    }
}
