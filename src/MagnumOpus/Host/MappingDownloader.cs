using System;
using System.Collections;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using MagnumOpus.Names;

namespace MagnumOpus.Host
{
    /// <summary>
    /// Fetches the community's intermediary mapping for a game build the release does not bundle, on
    /// a background thread: the GitHub contents listing of QuintessentialOM/om_mappings/intermediary,
    /// the file whose name carries the build's MVID, parsed with the framework's own
    /// JavaScriptSerializer (in-box on .NET Framework; nothing shipped) and cached as our TSV under
    /// %LOCALAPPDATA%\MagnumOpus\mappings. The community has published within a day of each CE update
    /// so far, so an absent file is retried at the next launch. Off with "mappings.download": "false"
    /// in settings.json.
    /// </summary>
    internal static class MappingDownloader
    {
        private const string ListUrl = "https://api.github.com/repos/QuintessentialOM/om_mappings/contents/intermediary";

        public static bool Enabled => HostConfig.GetBool("mappings.download", true);

        /// <summary>Start the fetch; <paramref name="done"/> runs on the background thread with the
        /// cached path once a mapping is in place (never when there is none or the fetch failed).</summary>
        public static void Start(Guid mvid, Action<string> done)
        {
            var thread = new Thread(() => Run(mvid, done)) { IsBackground = true, Name = "MagnumOpusMappingDownload" };
            thread.Start();
        }

        private static void Run(Guid mvid, Action<string> done)
        {
            ModHostImpl.Notices.Enqueue(HostNotice.MappingDownloading);
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                string id = mvid.ToString("D");
                string listing = Get(ListUrl);
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
                string url = null, name = null;
                foreach (var entry in (IEnumerable)serializer.DeserializeObject(listing))
                {
                    var d = entry as IDictionary;
                    var n = d?["name"] as string;
                    if (n != null && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && n.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        name = n;
                        url = d["download_url"] as string;
                        break;
                    }
                }
                if (url == null)
                {
                    Log.Info("[names] the community has not published a mapping for build " + id + " yet; the matched names stay in use.");
                    ModHostImpl.Notices.Enqueue(HostNotice.MappingNotPublished);
                    return;
                }
                Log.Info("[names] downloading the community mapping " + name + ".");
                var map = ObfMap.FromJsonModel(serializer.DeserializeObject(Get(url)), mvid);
                string path = NameSources.CachedPath(mvid);
                map.WriteTsvFile(path);
                Log.Info("[names] cached " + map.Classes.Count + " class mappings at " + path + ".");
                done(path);
            }
            catch (Exception ex)
            {
                Log.Warning("[names] mapping download failed (" + ex.GetType().Name + ": " + ex.Message + "); retried next launch.");
                ModHostImpl.Notices.Enqueue(HostNotice.MappingDownloadFailed);
            }
        }

        private static string Get(string url)
        {
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.UserAgent] = "MagnumOpus/" + ModHostImpl.Version;
                client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                return client.DownloadString(url);
            }
        }
    }
}
