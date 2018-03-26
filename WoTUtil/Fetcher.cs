using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using log4net;
using Negri.Wot.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Negri.Wot
{
    /// <summary>
    ///     Read information from the Web
    /// </summary>
    public class Fetcher
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Fetcher));

        private const int MaxTry = 10;

        private readonly string _cacheDirectory;

        private DateTime _lastWebFetch = DateTime.MinValue;

        public Fetcher(string cacheDirectory = null)
        {
            _cacheDirectory = cacheDirectory ?? Path.GetTempPath();

            WebFetchInterval = TimeSpan.Zero;
        }

        /// <summary>
        ///     AppId to query the WG API
        /// </summary>
        public string ApplicationId { set; private get; } = "demo";
        

        public TimeSpan WebFetchInterval { set; private get; }
        
        /// <summary>
        ///     Find a clan, given the tag
        /// </summary>
        public long? FindClan(string clanTag)
        {
            Log.DebugFormat("Searching data for clan [{0}]...", clanTag);
            string server = "api-xbox-console.worldoftanks.com";
            
            string requestUrl =
                $"https://{server}/wotx/clans/list/?application_id={ApplicationId}&search={clanTag}&limit=1";

            var json =
                GetContent($"wcl.FindClan.{clanTag}.json", requestUrl, TimeSpan.FromDays(1), false,
                    Encoding.UTF8).Content;

            var response = JsonConvert.DeserializeObject<ClansListResponse>(json);
            if (response.IsError)
            {
                Log.Error(response.Error);
                return null;
            }

            // Deve coincidir
            if (response.Clans.Length != 1)
            {
                Log.Warn($"There are {response.Clans.Length} responses on the search for [{clanTag}].");
                return null;
            }

            var found = response.Clans[0];
            if (string.Compare(clanTag, found.Tag, StringComparison.OrdinalIgnoreCase) != 0)
            {
                Log.Warn($"The seach for [{clanTag}] found only [{found.Tag}], and is not a match.");
                return null;
            }
            
            return found.ClanId;
        }

        /// <summary>
        ///     Devolve o jogador a partir da Gamertag
        /// </summary>
        public Player GetPlayerByGamerTag(string gamerTag)
        {
            Log.DebugFormat("Searching [{0}]...", gamerTag);

            if (string.IsNullOrWhiteSpace(gamerTag))
            {
                Log.WarnFormat("Empty Gamer Tag");
                return null;
            }

            if (gamerTag.Length > 15)
            {
                Log.Warn($"Gamer Tag [{gamerTag}] is longer than 15 characters.");
                return null;
            }

            string server = "api-xbox-console.worldoftanks.com";
            
            string url = $"https://{server}/wotx/account/list/?application_id={ApplicationId}&search={gamerTag}&type=exact";
            var d =
                GetContent($"wcl.AccountList.{gamerTag.SanitizeForFileName()}.json", url, TimeSpan.FromDays(7), false, Encoding.UTF8);

            var json = d.Content;                   
            var result = JObject.Parse(json);

            var status = (string)result["status"];
            if (status != "ok")
            {
                Log.WarnFormat("Error on query for [{0}].", gamerTag);
                return null;
            }

            var count = (int)result["meta"]["count"];
            if (count < 1)
            {
                Log.WarnFormat("Not found gamer tag [{0}].", gamerTag);
                return null;
            }

            if (count >= 1)
            {
                var suggested = (string)result["data"][0]["nickname"];
                if (!suggested.Equals(gamerTag, StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.WarnFormat("There are {0} results for the Gamer Tag [{1}], bu the first is [{2}].", count, gamerTag, suggested);
                    return null;
                }
            }

            var player = new Player
            {
                Id = (long)result["data"][0]["account_id"],
                GamerTag = (string)result["data"][0]["nickname"]
            };

            // Find the current clan, if any
            url = $"https://{server}/wotx/clans/accountinfo/?application_id={ApplicationId}&account_id={player.Id}&extra=clan";
            json =
                GetContent($"wcl.ClansAccountinfo.{gamerTag}.json", url, TimeSpan.FromHours(2), false, Encoding.UTF8)
                    .Content;
            result = JObject.Parse(json);
            count = (int)result["meta"]["count"];
            if (count == 1)
            {
                // Existem dados de clã
                var clanDetails = result["data"][player.Id.ToString()];
                if (clanDetails.Children().Any())
                {
                    player.CurrentClanId = (long?)result["data"][player.Id.ToString()]["clan_id"];
                    if (player.CurrentClanId.HasValue)
                    {
                        player.CurrentClanTag = (string)result["data"][player.Id.ToString()]["clan"]["tag"];
                    }
                    else
                    {
                        Log.Debug($"No current clan for [{gamerTag}]");
                    }
                }
                else
                {
                    Log.Debug($"No current clan for [{gamerTag}]");
                }
            }
            else
            {
                Log.Debug($"No clan for [{gamerTag}]");
            }

            player.Moment = d.Moment;

            return player;
        }


        #region Infra

       
        private WebContent GetContent(string cacheFileTitle, string url, TimeSpan maxCacheAge, bool noWait,
            Encoding encoding = null)
        {
            Log.DebugFormat("Retrieving '{0}' ...", url);

            encoding = encoding ?? Encoding.UTF8;

            var cacheFileName = Path.Combine(_cacheDirectory, cacheFileTitle);
            if (!File.Exists(cacheFileName))
            {
                Log.Debug("...never retrieved before...");
                return GetContentFromWeb(cacheFileName, url, noWait, encoding);
            }

            var fi = new FileInfo(cacheFileName);
            var moment = fi.LastWriteTimeUtc;
            var age = DateTime.UtcNow - moment;
            if (age > maxCacheAge)
            {
                Log.DebugFormat("...file on cache '{0}' from {1:yyyy-MM-dd HH:mm} expired with {2:N0}h...",
                    cacheFileTitle, moment, age.TotalHours);

                return GetContentFromWeb(cacheFileName, url, noWait, encoding);
            }

            Log.Debug("...got from cache.");
            return new WebContent(File.ReadAllText(cacheFileName, encoding)) {Moment = moment};
        }

        private WebContent GetContentFromWeb(string cacheFileName, string url, bool noWait,
            Encoding encoding)
        {
            var timeSinceLastFetch = DateTime.UtcNow - _lastWebFetch;
            var waitTime = WebFetchInterval - timeSinceLastFetch;
            var waitTimeMs = Math.Max((int) waitTime.TotalMilliseconds, 0);
            if (!noWait & (waitTimeMs > 0))
            {
                Log.DebugFormat("...waiting {0:N1}s to use the web...", waitTimeMs / 1000.0);
                Thread.Sleep(waitTimeMs);
            }

            Exception lastException = new ApplicationException("Flow control error!");

            for (var i = 0; i < MaxTry; ++i)
            {
                try
                {
                    var moment = DateTime.UtcNow;
                    var sw = Stopwatch.StartNew();

                    var webClient = new WebClient();
                    webClient.Headers.Add("user-agent",
                        "WCLUtility by JP Negri at negrijp _at_ gmail.com");
                    var bytes = webClient.DownloadData(url);
                    var webTime = sw.ElapsedMilliseconds;

                    var content = Encoding.UTF8.GetString(bytes);

                    // Escreve em cache
                    sw.Restart();

                    for (int j = 0; j < MaxTry; ++j)
                    {
                        try
                        {
                            File.WriteAllText(cacheFileName, content, encoding);
                            break;
                        }
                        catch (IOException ex)
                        {
                            if (j < MaxTry - 1)
                            {
                                Log.Warn("...waiting before retry.");
                                Thread.Sleep(TimeSpan.FromSeconds(j * j * 0.1));
                            }
                            else
                            {
                                // Ficou sem cache
                                Log.Error(ex);
                            }
                        }
                    }


                    var cacheWriteTime = sw.ElapsedMilliseconds;

                    if (!noWait)
                    {
                        _lastWebFetch = moment;
                    }

                    Log.DebugFormat("...got from web in {0}ms and wrote in cache in {1}ms.", webTime, cacheWriteTime);
                    return new WebContent(content) {Moment = moment};
                }
                catch (WebException ex)
                {
                    Log.Warn(ex);
                    if (ex.Status == WebExceptionStatus.ProtocolError)
                    {
                        var response = ex.Response as HttpWebResponse;
                        if (response?.StatusCode == HttpStatusCode.NotFound)
                        {
                            throw;
                        }
                    }

                    if (i < MaxTry - 1)
                    {
                        Log.Warn("...waiting before retry.");
                        Thread.Sleep(TimeSpan.FromSeconds(i * i * 2));
                    }

                    lastException = ex;
                }
            }

            throw lastException;
        } 

        /// <summary>
        ///     Conteudo obtido na Web (ou cache dela)
        /// </summary>
        private class WebContent
        {
            public WebContent(string content)
            {
                Content = content;
                Moment = DateTime.UtcNow;
            }

            /// <summary>
            ///     O conteudo em si
            /// </summary>
            public string Content { get; }

            /// <summary>
            ///     O momento em que o dado foi pego
            /// </summary>
            public DateTime Moment { get; set; }
        }

        #endregion
    }
}