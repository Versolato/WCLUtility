using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Negri.Wot.Api;
using System.Collections.Concurrent;
using System.Threading;
using Negri.Wot.Tanks;

namespace Negri.Wot.Wcl
{
    public class WclValidator
    {
        /// <summary>
        ///     Log
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(WclValidator));

        /// <summary>
        /// The App Id to query WG API
        /// </summary>
        public string AppId { private get; set; }

        /// <summary>
        /// Output file
        /// </summary>
        public string ResultFile { get; private set; }

        /// <summary>
        /// 100% valid records
        /// </summary>
        public int ValidRecords { get; private set; }

        /// <summary>
        /// Total records
        /// </summary>
        public int TotalRecords { get; private set; }

        /// <summary>
        /// The current progress
        /// </summary>
        public double Progress { get; private set; } = 0.0;

        /// <summary>
        /// Current status
        /// </summary>
        public string Status { get; private set; }

        /// <summary>
        /// The WN8 Expected Values
        /// </summary>
        public Wn8ExpectedValues Wn8ExpectedValues { get; private set; }

        /// <summary>
        /// If the performance of each player and the clan should be calculated
        /// </summary>
        public bool CalculatePerformance { private get; set; } = false;

        /// <summary>
        /// The last fatal error, if any
        /// </summary>
        public string LastFatalError { get; private set; }

        private void SetInfo(string msg)
        {
            Log.Info(msg);
            Status = msg;            
        }

        private void SetWarn(string msg)
        {
            Log.Warn(msg);
            Status = msg;
        }

        private void SetError(string msg)
        {
            Log.Error(msg);
            Status = msg;
        }

        public bool Run(string originalFile)
        {
            try
            {
                LastFatalError = string.Empty;
                Progress = 0.0;

                SetInfo($"Parsing file '{originalFile}'...");
                var allLines = File.ReadAllLines(originalFile, Encoding.UTF8);
                var records = Parse(allLines).ToArray();
                SetInfo($"{records.Length:N0} records read from file...");
                TotalRecords = records.Length;

                if (records.Length == 0)
                {
                    LastFatalError = "No records where found!"; 
                    SetError("No records where found!");
                    return false;
                }

                Progress = 0.10;

                SetInfo("Checking for basic errors...");
                var invalids = RunBasicValidations(records);
                if (invalids > 0)
                {
                    SetError($"{invalids:N0} records invalidated on the 1st pass (Basic Errors).");
                }

                Progress = 0.20;

                SetInfo("Checking for clans errors...");
                invalids = GetClanIds(records, 0.20, 0.50);
                if (invalids > 0)
                {
                    SetError($"{invalids:N0} records invalidated on the 2nd pass (Checking Clans).");
                }

                Progress = 0.45;

                if (CalculatePerformance)
                {
                    SetInfo("Retrieving WN8 Reference Values...");
                    GetWn8ReferenceValues();
                }

                Progress = 0.50;

                SetInfo("Checking for player errors...");
                invalids = GetPlayerIds(records, 0.50, 0.90);
                if (invalids > 0)
                {
                    SetError($"{invalids:N0} records invalidated on the 3rd pass (Checking Users).");
                }

                SetInfo($"{records.Count(r => r.IsValid)} valids records");

                Progress = 0.90;

                SetInfo("Checking for relational errors...");
                invalids = CheckRelationalErrors(records);
                if (invalids > 0)
                {
                    SetError($"{invalids:N0} records invalidated on the 4rd pass (Checking Relations).");
                }

                Progress = 0.95;

                ValidRecords = records.Count(r => r.IsValid);
                SetInfo($"{ValidRecords:N0} valids records");

                SetInfo("Writing output file...");
                WriteFile(originalFile, records);
                SetInfo("All done!");

                Progress = 1.0;
                return true;
            }
            catch(Exception ex)
            {
                LastFatalError = ex.Message;
                Log.Error(nameof(Run), ex);
                SetError(ex.Message);
                return false;
            }
        }

        private void GetWn8ReferenceValues()
        {
            var fetcher = new Fetcher
            {
                ApplicationId = AppId
            };

            Wn8ExpectedValues = fetcher.GetWn8ExpectedValues();
        }

        private static int CheckRelationalErrors(Record[] records)
        {
            int invalids = 0;
            var clans = new Dictionary<long, Clan>();

            // Put players on clans
            foreach (var r in records.Where(r => r.ClanId.HasValue && r.PlayerId.HasValue))
            {
                Debug.Assert(r.ClanId != null, "r.ClanId != null");
                if (!clans.TryGetValue(r.ClanId.Value, out var clan))
                {
                    clan = new Clan
                    {
                        ClanId = r.ClanId.Value,
                        Tag = r.ClanTag
                    };
                    clans.Add(r.ClanId.Value, clan);
                }

                Debug.Assert(r.PlayerId != null, "r.InGameId != null");
                var added = clan.AddMember(r.PlayerId.Value);
                if (!added)
                {
                    // Duplicate registry
                    r.AddInvalidReason($"The player [{r.GamerTag}] was already on the [{r.ClanTag}] clan.");
                    ++invalids;
                }
            }

            // Check to see if a player went on more than one clan
            foreach (var r in records.Where(r => r.ClanId.HasValue && r.PlayerId.HasValue))
            {
                Debug.Assert(r.ClanId != null, "r.ClanId != null");
                var clan = clans[r.ClanId.Value];

                foreach (var otherClan in clans.Values.Where(c => c.ClanId != clan.ClanId))
                {
                    Debug.Assert(r.PlayerId != null, "r.InGameId != null");
                    if (otherClan.HasMember(r.PlayerId.Value))
                    {
                        r.AddInvalidReason($"The player [{r.GamerTag}], member of the [{r.ClanTag}] clan, also appears on the clan [{otherClan.Tag}].");
                        ++invalids;
                    }
                }
            }

            return invalids;
        }

        private void WriteFile(string originalFile, Record[] records)
        {
            var dir = Path.GetDirectoryName(originalFile);
            var baseName = Path.GetFileName(originalFile);
            Debug.Assert(dir != null, nameof(dir) + " != null");
            var newFile = Path.Combine(dir, $"valid.{baseName}");

            var type = records.FirstOrDefault()?.Type ?? Record.RecordType.Full;

            var sb = new StringBuilder();
            sb.AppendLine(type == Record.RecordType.Full ? Record.FullLineHeader : Record.SimpleLineHeader);
            foreach (var r in records)
            {
                sb.AppendLine(r.ToString(type));
            }

            File.WriteAllText(newFile, sb.ToString(), Encoding.UTF8);
            SetInfo($"Validated file wrote on '{newFile}'.");

            ResultFile = newFile;
        }

        private int GetPlayerIds(Record[] records, double startProgress, double finalProgress)
        {
            double incProgress = (finalProgress - startProgress) / records.Length;

            const int maxParallel = 4;
            var fetchers = new ConcurrentQueue<Fetcher>();
            for (int i = 0; i < maxParallel * 4; i++)
            {
                var fetcher = new Fetcher
                {
                    ApplicationId = AppId
                };
                fetchers.Enqueue(fetcher);
            }

            int done = 0;
            int invalidCount = 0;
            var a = records.Where(r => !string.IsNullOrWhiteSpace(r.GamerTag)).ToArray();
            Parallel.For(0, a.Length, new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, i =>
            {
                var r = a[i];

                Progress = startProgress + incProgress * done;
                Interlocked.Increment(ref done);

                Fetcher fetcher;
                while (!fetchers.TryDequeue(out fetcher))
                {
                    Thread.SpinWait(5000);
                }

                var p = fetcher.GetPlayerByGamerTag(r.GamerTag);

                if (p == null)
                {
                    r.AddInvalidReason($"The inGameName [{r.GamerTag}] could not be found.");
                    Interlocked.Increment(ref invalidCount);
                }
                else
                {
                    r.GamerTag = p.GamerTag;
                    r.Player = p;
                }

                if ((p != null) && (Wn8ExpectedValues != null))
                {
                    // retrieve performance on each tank of the player
                    p.Tanks = fetcher.GetTanksForPlayer(p.Id).ToArray();
                    p.CalculatePerformance(Wn8ExpectedValues);
                }

                fetchers.Enqueue(fetcher);
            });

            return invalidCount;
        }

        private int GetClanIds(Record[] records, double startProgress, double finalProgress)
        {
            double incProgress = (finalProgress - startProgress) / records.Length;

            var fetcher = new Fetcher
            {
                ApplicationId = AppId
            };

            var clanIds = new Dictionary<string, long>();
            var notFound = new HashSet<string>();

            int done = 0;
            int invalidCount = 0;
            foreach (var r in records.Where(r => !string.IsNullOrEmpty(r.ClanTag)))
            {
                Progress = startProgress + incProgress * (done++);

                if (notFound.Contains(r.ClanTag))
                {
                    r.AddInvalidReason($"Could not find a clan with the tag [{r.ClanTag}]");
                    invalidCount++;
                    continue;
                }

                // from cache?
                if (clanIds.TryGetValue(r.ClanTag, out var existingId))
                {
                    if (!string.IsNullOrWhiteSpace(r.ClanUrl))
                    {
                        r.ClanUrl = $"https://console.worldoftanks.com/en/clans/xbox/{r.ClanTag}/";
                    }

                    r.ClanTagFromUrl = r.ClanTag;
                    r.ClanId = existingId;
                    continue;
                }

                // search by informed tag
                var clanId = fetcher.FindClan(r.ClanTag);
                if (clanId != null)
                {
                    if (!string.IsNullOrWhiteSpace(r.ClanUrl))
                    {
                        r.ClanUrl = $"https://console.worldoftanks.com/en/clans/xbox/{r.ClanTag}/";
                    }

                    r.ClanTagFromUrl = r.ClanTag;
                    r.ClanId = clanId.Value;
                    clanIds[r.ClanTag] = clanId.Value;
                    continue;
                }

                notFound.Add(r.ClanTag);

                // Maybe the URLs was right...
                if (!string.IsNullOrWhiteSpace(r.ClanTagFromUrl))
                {
                    if (notFound.Contains(r.ClanTagFromUrl))
                    {
                        r.AddInvalidReason($"Could not find a clan with the tag [{r.ClanTagFromUrl}] (extracted from the URL)");
                        invalidCount++;
                        continue;
                    }

                    if (clanIds.TryGetValue(r.ClanTagFromUrl, out existingId))
                    {
                        SetWarn($"The teamName [{r.ClanTag}] could not be found. Matched by the Url [{r.ClanTagFromUrl}]");
                        r.ClanUrl = $"https://console.worldoftanks.com/en/clans/xbox/{r.ClanTagFromUrl}/";
                        r.ClanTag = r.ClanTagFromUrl;
                        r.ClanId = existingId;
                        continue;
                    }

                    clanId = fetcher.FindClan(r.ClanTagFromUrl);
                    if (clanId != null)
                    {
                        SetWarn($"The teamName [{r.ClanTag}] could not be found. Matched by the Url [{r.ClanTagFromUrl}]");
                        r.ClanUrl = $"https://console.worldoftanks.com/en/clans/xbox/{r.ClanTagFromUrl}/";
                        r.ClanTag = r.ClanTagFromUrl;
                        r.ClanId = clanId.Value;
                        clanIds[r.ClanTag] = clanId.Value;
                        continue;
                    }

                    notFound.Add(r.ClanTag);
                }

                r.AddInvalidReason($"Could not find a clan with the tag [{r.ClanTag}]");
                invalidCount++;
            }

            return invalidCount;
        }

        private int RunBasicValidations(IEnumerable<Record> records)
        {
            int invalidCount = 0;
            foreach (var r in records)
            {
                r.Validate();
                if (!r.IsValid)
                {
                    ++invalidCount;
                    SetWarn($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReasons}");
                }                
            }

            return invalidCount;
        }

        private IEnumerable<Record> Parse(IEnumerable<string> allLines)
        {
            var a = allLines.ToArray();
            for (var i = 1; i < a.Length; i++)
            {
                string line = a[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("//"))
                {
                    continue;
                }

                var r = Record.Parse(line, i + 1);

                if (r == null)
                {
                    SetError($"Line {i + 1} was ignored.");
                    continue;
                }

                yield return r;
            }
        }

    }
}
