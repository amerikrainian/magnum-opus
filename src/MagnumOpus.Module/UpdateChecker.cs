using System;
using System.Net;
using System.Threading;
using MagnumOpus.Core;

namespace MagnumOpus.Module
{
    /// <summary>
    /// The launch update check: the latest GitHub release of amerikrainian/magnum-opus against the
    /// running version, on a background thread; the module speaks "update X available" once it
    /// lands with a newer release. A 404 (no release yet) or no network is a log line only.
    /// </summary>
    internal sealed class UpdateChecker
    {
        private const string ApiUrl = "https://api.github.com/repos/amerikrainian/magnum-opus/releases/latest";

        private volatile string _newerVersion;

        public string NewerVersion => _newerVersion;

        public void Start(string local)
        {
            new Thread(() => Check(local)) { IsBackground = true, Name = "MagnumOpusUpdateCheck" }.Start();
        }

        private void Check(string local)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                string json;
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "MagnumOpus-mod";
                    client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                    json = client.DownloadString(ApiUrl);
                }
                string remote = UpdateCheck.LatestVersion(json);
                if (remote == null) { CoreLog.Warning("update check: release payload named no version"); return; }
                if (UpdateCheck.IsNewer(remote, local))
                {
                    CoreLog.Info("update check: " + remote + " available (running " + local + ")");
                    _newerVersion = remote;
                }
                else
                {
                    CoreLog.Info("update check: up to date (latest " + remote + ", running " + local + ")");
                }
            }
            catch (Exception e)
            {
                CoreLog.Info("update check: no answer (" + e.Message + ")");
            }
        }
    }
}
