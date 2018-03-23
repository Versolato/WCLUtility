using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Negri.Wcl.Api;

namespace Negri.Wcl
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static int Main(string[] args)
        {
            try
            {
                if (args == null || args.Length < 2)
                {
                    Log.Error("Missing arguments!");
                    Log.Warn("Try\r\nWCLUtility validate Battlefy_master_file.csv");
                    return 2;
                }

                if (!args[0].Equals("Validade", StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.Error("Invalid command!");
                    Log.Warn("Try\r\nWCLUtility Validate Battlefy_master_file.csv");
                    return 3;
                }

                string originalFile = args[1];
                if (!File.Exists(originalFile))
                {
                    Log.Error("File not found!");
                    Log.Warn($"Could not find the file '{originalFile}'");
                    return 4;
                }

                Log.Info($"Parsing file '{originalFile}'...");
                var allLines = File.ReadAllLines(originalFile, Encoding.UTF8);
                var records = Parse(allLines).ToArray();
                Log.Info($"{records.Length:N0} records read from file...");

                Log.Info("Checking for basic errors...");
                var invalids = RunBasicValidations(records);
                if (invalids > 0)
                {
                    Log.Error($"{invalids:N0} records invalidated on the 1st pass (Basic Errors).");
                }

                Log.Info("Checking for clans errors...");
                invalids = GetClanIds(records);
                if (invalids > 0)
                {
                    Log.Error($"{invalids:N0} records invalidated on the 2nd pass (Checking Clans).");
                }

                Log.Info("Checking for player errors...");
                invalids = GetPlayerIds(records);
                if (invalids > 0)
                {
                    Log.Error($"{invalids:N0} records invalidated on the 3rd pass (Checking Users).");
                }

                Log.Info($"{records.Count(r => r.IsValid)} valids records");

                Log.Info("Checking for relational errors...");
                invalids = CheckRelationalErrors(records);
                if (invalids > 0)
                {
                    Log.Error($"{invalids:N0} records invalidated on the 4rd pass (Checking Relations).");
                }

                Log.Info($"{records.Count(r => r.IsValid)} valids records");

                Log.Info("Writing output file...");
                WriteFile(originalFile, records);

                Log.Info("Bye!");
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal("Unhandled Exception", ex);
                return 1;
            }
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

        private static void WriteFile(string originalFile, Record[] records)
        {
            var dir = Path.GetDirectoryName(originalFile);
            var baseName = Path.GetFileName(originalFile);
            Debug.Assert(dir != null, nameof(dir) + " != null");
            var newFile = Path.Combine(dir, $"valid.{baseName}");

            var sb = new StringBuilder();
            sb.AppendLine(Record.LineHeader);
            foreach (var r in records)
            {
                sb.AppendLine(r.ToString());
            }

            File.WriteAllText(newFile, sb.ToString(), Encoding.UTF8);
            Log.Info($"Validated file wrote on '{newFile}'.");
        }

        private static int GetPlayerIds(Record[] records)
        {
            string appId = GetApplicationId();

            const int maxParallel = 4;
            var fetchers = new ConcurrentQueue<Fetcher>();
            for (int i = 0; i < maxParallel * 4; i++)
            {
                var fetcher = new Fetcher
                {
                    ApplicationId = appId
                };
                fetchers.Enqueue(fetcher);
            }

            int invalidCount = 0;
            var a = records.Where(r => !string.IsNullOrWhiteSpace(r.GamerTag)).ToArray();
            Parallel.For(0, a.Length, new ParallelOptions {MaxDegreeOfParallelism = maxParallel}, i =>
            {
                var r = a[i];

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

                fetchers.Enqueue(fetcher);
            });

            return invalidCount;
        }

        private static string GetApplicationId()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!Directory.Exists(directory))
            {
                Log.Warn("You are using the 'demo' API Key. The application may not work at all! (Not founf the assembly directory)");
                return "demo";
            }

            var file = Path.Combine(directory, "AppId.txt");
            if (!File.Exists(file))
            {
                Log.Warn($"You are using the 'demo' API Key. The application may not work at all! (File '{file}' does not exists)");
                return "demo";
            }

            return File.ReadAllText(file, Encoding.UTF8).Trim();
        }

        private static int GetClanIds(Record[] records)
        {
            var fetcher = new Fetcher
            {
                ApplicationId = GetApplicationId()
            };

            var clanIds = new Dictionary<string, long>();
            var notFound = new HashSet<string>();

            int invalidCount = 0;
            foreach (var r in records.Where(r => !string.IsNullOrEmpty(r.ClanTag)))
            {
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
                        Log.Warn($"The teamName [{r.ClanTag}] could not be found. Matched by the Url [{r.ClanTagFromUrl}]");
                        r.ClanUrl = $"https://console.worldoftanks.com/en/clans/xbox/{r.ClanTagFromUrl}/";
                        r.ClanTag = r.ClanTagFromUrl;
                        r.ClanId = existingId;
                        continue;
                    }

                    clanId = fetcher.FindClan(r.ClanTagFromUrl);
                    if (clanId != null)
                    {
                        Log.Warn($"The teamName [{r.ClanTag}] could not be found. Matched by the Url [{r.ClanTagFromUrl}]");
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

        private static int RunBasicValidations(IEnumerable<Record> records)
        {
            int invalidCount = 0;
            foreach (var r in records)
            {
                if (!r.Validate())
                {
                    ++invalidCount;
                    Log.Warn($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReasons}");
                }
            }

            return invalidCount;
        }

        private static IEnumerable<Record> Parse(IEnumerable<string> allLines)
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
                    Log.Error($"Line {i + 1} was ignored.");
                    continue;
                }

                yield return r;
            }
        }
    }
}