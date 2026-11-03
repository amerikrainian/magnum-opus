using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MagnumOpus
{
    /// <summary>
    /// The mod's logger: %LOCALAPPDATA%\MagnumOpus\magnumopus.log, a fresh file per launch (the one
    /// path to give testers). Opus Magnum has no engine log surface to borrow, and the stock exe is a
    /// GUI app, so console output goes nowhere. Lines are also kept in memory for the dev server's
    /// GET /log. Thread-safe: the game thread and the dev server's HTTP thread both log.
    /// </summary>
    public static class Log
    {
        private static readonly object Gate = new object();
        private static string _path;
        private static readonly List<string> Recent = new List<string>();
        private static long _first; // sequence number of Recent[0]
        private const int Keep = 4000;

        public static string Path => _path;

        public static void Init()
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagnumOpus");
                Directory.CreateDirectory(dir);
                _path = System.IO.Path.Combine(dir, "magnumopus.log");
                File.WriteAllText(_path, "Magnum Opus log, " + DateTime.Now.ToString("s") + Environment.NewLine);
            }
            catch { _path = null; }
        }

        public static void Info(string message) => Write("INFO ", message);
        public static void Warning(string message) => Write("WARN ", message);
        public static void Error(string message) => Write("ERROR", message);
        public static void Error(string message, Exception ex) => Write("ERROR", message + Environment.NewLine + ex);

        private static void Write(string level, string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + level + " " + message;
            lock (Gate)
            {
                Recent.Add(line);
                if (Recent.Count > Keep) { Recent.RemoveRange(0, Recent.Count - Keep); _first = NextSeq - Recent.Count; }
                if (_path == null) return;
                try { File.AppendAllText(_path, line + Environment.NewLine); } catch { }
            }
        }

        private static long NextSeq => _first + Recent.Count;

        /// <summary>Lines from sequence number <paramref name="since"/> on, optionally only those
        /// containing <paramref name="grep"/>; <paramref name="next"/> is the cursor to pass next time.</summary>
        public static string Since(long since, string grep, out long next)
        {
            var sb = new StringBuilder();
            lock (Gate)
            {
                next = NextSeq;
                long start = Math.Max(since, _first);
                for (long i = start; i < next; i++)
                {
                    string line = Recent[(int)(i - _first)];
                    if (string.IsNullOrEmpty(grep) || line.IndexOf(grep, StringComparison.OrdinalIgnoreCase) >= 0)
                        sb.Append(line).Append('\n');
                }
            }
            return sb.ToString();
        }
    }
}
