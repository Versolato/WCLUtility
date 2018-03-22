using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Negri.Wcl.Api;

namespace Negri.Wcl
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static int Main(string[] args)
        {
            try
            {
                if ((args == null) || (args.Length < 2))
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

        private static int CheckRelationalErrors(RawRecord[] records)
        {
            int invalids = 0;
            var clans = new Dictionary<long, Clan>();

            // Put players on clans
            foreach (var r in records.Where(r => r.ClanId.HasValue && r.InGameId.HasValue))
            {
                Debug.Assert(r.ClanId != null, "r.ClanId != null");
                if (!clans.TryGetValue(r.ClanId.Value, out var clan))
                {
                    clan = new Clan
                    {
                        ClanId = r.ClanId.Value,
                        Tag = r.TeamName
                    };
                    clans.Add(r.ClanId.Value, clan);
                }

                Debug.Assert(r.InGameId != null, "r.InGameId != null");
                var added = clan.AddMember(r.InGameId.Value);
                if (!added)
                {
                    // Duplicate registry
                    r.IsValid = false;
                    r.InvalidReason = $"The player [{r.InGameName}] was already on the [{r.TeamName}] clan.";
                    ++invalids;
                    Log.Error($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");
                }
            }

            // Check the On Last Season Top 32
            foreach (var r in records.Where(r => r.OnLastSeasonTop32Players.Any() && r.ClanId.HasValue))
            {
                Debug.Assert(r.ClanId != null, "r.ClanId != null");
                if (!clans.TryGetValue(r.ClanId.Value, out var clan))
                {
                    // can happen, on already invalid records
                    continue;
                }

                foreach (var onLastSeasonTop32Player in r.OnLastSeasonTop32Players)
                {
                    if (!clan.HasMember(onLastSeasonTop32Player.Id))
                    {
                        r.IsValid = false;
                        r.InvalidReason = $"The player [{onLastSeasonTop32Player.GamerTag}] was claimed to be on the Last Season top 32, but he is not a member of the [{r.TeamName}] clan.";
                        ++invalids;
                        Log.Error($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");
                    }
                }
            }

            // Check to see if a player went on more than one clan
            foreach (var r in records.Where(r => r.ClanId.HasValue && r.InGameId.HasValue))
            {
                Debug.Assert(r.ClanId != null, "r.ClanId != null");
                var clan = clans[r.ClanId.Value];

                foreach (var otherClan in clans.Values.Where(c => c.ClanId != clan.ClanId))
                {
                    Debug.Assert(r.InGameId != null, "r.InGameId != null");
                    if (otherClan.HasMember(r.InGameId.Value))
                    {
                        r.IsValid = false;
                        r.InvalidReason = $"The player [{r.InGameName}], member of the [{r.TeamName}] clan, also appears on the clan [{otherClan.Tag}].";
                        ++invalids;
                        Log.Error($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");
                    }
                }

            }


            return invalids;
        }

        private static void WriteFile(string originalFile, RawRecord[] records)
        {
            var dir = Path.GetDirectoryName(originalFile);
            var baseName = Path.GetFileName(originalFile);
            Debug.Assert(dir != null, nameof(dir) + " != null");
            var newFile = Path.Combine(dir, $"valid.{baseName}");

            var sb = new StringBuilder();
            sb.AppendLine(RawRecord.LineHeader);
            foreach (var r in records)
            {
                sb.AppendLine(r.ToString());
            }

            File.WriteAllText(newFile, sb.ToString(), Encoding.UTF8);
            Log.Info($"Validated file wrote on '{newFile}'.");
        }

        private static int GetPlayerIds(RawRecord[] records)
        {
            string appId = GetApplicationId();

            const int maxParallel = 4;
            var fetchers = new ConcurrentQueue<Fetcher>();
            for (int i = 0; i < maxParallel*4; i++)
            {
                var fetcher = new Fetcher
                {
                    ApplicationId = appId
                };
                fetchers.Enqueue(fetcher);
            }
           
            int invalidCount = 0;
            var a = records.Where(r => !string.IsNullOrWhiteSpace(r.InGameName)).ToArray();
            Parallel.For(0, a.Length, new ParallelOptions() {MaxDegreeOfParallelism = maxParallel}, i =>
            {
                var r = a[i];

                Fetcher fetcher;
                while (!fetchers.TryDequeue(out fetcher))
                {
                    Thread.SpinWait(5000);
                }

                var p = fetcher.GetPlayerByGamerTag(r.InGameName);
                
                if (p == null)
                {
                    r.IsValid = false;
                    r.InvalidReason = $"The inGameName [{r.InGameName}] could not be found.";
                    Interlocked.Increment(ref invalidCount);
                    Log.Error($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");                    
                }
                else
                {
                    r.InGameName = p.GamerTag;
                    r.Player = p;
                }

                if (r.OnLastSeasonTop32.Any())
                {
                    for (int j = 0; j < r.OnLastSeasonTop32.Length; j++)
                    {
                        var s = r.OnLastSeasonTop32[j];
                        p = fetcher.GetPlayerByGamerTag(s);
                        if (p == null)
                        {
                            r.IsValid = false;
                            r.InvalidReason = $"The OnLastSeasonTop32 Gamer Tag [{s}], position {j+1}, could not be found.";
                            Interlocked.Increment(ref invalidCount);
                            Log.Error($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");
                        }
                        else
                        {
                            r.OnLastSeasonTop32Players.Add(p);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(r.TeamContactGamerTag))
                {
                    p = fetcher.GetPlayerByGamerTag(r.TeamContactGamerTag);

                    if (p == null)
                    {
                        r.IsValid = false;
                        r.InvalidReason = $"The Team Contact Gamer Tag [{r.TeamContactGamerTag}] could not be found.";
                        Interlocked.Increment(ref invalidCount);
                        Log.Error($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");
                    }
                    else
                    {
                        r.TeamContactGamerTag = p.GamerTag;
                        r.TeamContactPlayer = p;
                    }
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

        private static int GetClanIds(RawRecord[] records)
        {
            var fetcher = new Fetcher
            {
                ApplicationId = GetApplicationId()
            };

            var clanIds = new Dictionary<string, long>();
            var notFound = new HashSet<string>();

            int invalidCount = 0;
            foreach (var r in records.Where(r => r.IsValid))
            {
                if (notFound.Contains(r.TeamName))
                {
                    r.IsValid = false;
                    r.InvalidReason = $"Could not find a clan with the tag [{r.TeamName}]";
                    invalidCount++;
                    Log.Error($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");
                    continue;
                }

                // from cache?
                if (clanIds.TryGetValue(r.TeamName, out var existingId))
                {
                    if (!string.IsNullOrWhiteSpace(r.ClanMembershipUrl))
                    {
                        r.ClanMembershipUrl = $"https://console.worldoftanks.com/en/clans/xbox/{r.TeamName}/";
                    }                    
                    r.ClanTagFromUrl = r.TeamName;
                    r.ClanId = existingId;                    
                    continue;
                }

                // search by informed tag
                var clanId = fetcher.FindClan(r.TeamName);
                if (clanId != null)
                {
                    if (!string.IsNullOrWhiteSpace(r.ClanMembershipUrl))
                    {
                        r.ClanMembershipUrl = $"https://console.worldoftanks.com/en/clans/xbox/{r.TeamName}/";
                    }
                    r.ClanTagFromUrl = r.TeamName;
                    r.ClanId = clanId.Value;
                    clanIds[r.TeamName] = clanId.Value;
                    continue;
                }
                notFound.Add(r.TeamName);

                // Maybe the URLs was right...
                if (!string.IsNullOrWhiteSpace(r.ClanTagFromUrl))
                {
                    if (notFound.Contains(r.ClanTagFromUrl))
                    {
                        r.IsValid = false;
                        r.InvalidReason = $"Could not find a clan with the tag [{r.ClanTagFromUrl}] (extracted from the URL)";
                        invalidCount++;
                        Log.Error($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");
                        continue;
                    }

                    if (clanIds.TryGetValue(r.ClanTagFromUrl, out existingId))
                    {
                        Log.Warn($"The teamName [{r.TeamName}] could not be found. Matched by the Url [{r.ClanTagFromUrl}]");
                        r.ClanMembershipUrl = $"https://console.worldoftanks.com/en/clans/xbox/{r.ClanTagFromUrl}/";
                        r.TeamName = r.ClanTagFromUrl;
                        r.ClanId = existingId;
                        continue;
                    }

                    clanId = fetcher.FindClan(r.ClanTagFromUrl);
                    if (clanId != null)
                    {
                        Log.Warn($"The teamName [{r.TeamName}] could not be found. Matched by the Url [{r.ClanTagFromUrl}]");
                        r.ClanMembershipUrl = $"https://console.worldoftanks.com/en/clans/xbox/{r.ClanTagFromUrl}/";
                        r.TeamName = r.ClanTagFromUrl;
                        r.ClanId = clanId.Value;
                        clanIds[r.TeamName] = clanId.Value;
                        continue;
                    }
                    notFound.Add(r.TeamName);

                }

                r.IsValid = false;
                r.InvalidReason = $"Could not find a clan with the tag [{r.TeamName}]";

                invalidCount++;
                Log.Error($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");
            }

            // Check the parent clan
            foreach (var r in records.Where(r => r.IsValid && !string.IsNullOrWhiteSpace(r.ParentClanTag)))
            {
                if (notFound.Contains(r.ParentClanTag))
                {
                    r.IsValid = false;
                    r.InvalidReason = $"Could not find a parant clan with the tag [{r.ParentClanTag}]";
                    invalidCount++;
                    Log.Error($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");
                    continue;
                }

                if (clanIds.TryGetValue(r.ParentClanTag, out var existingId))
                {
                    r.ParentClanId = existingId;
                    continue;
                }

                r.IsValid = false;
                r.InvalidReason = $"Could not find a parent clan with the tag [{r.ParentClanTag}]";
                invalidCount++;
                Log.Error($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");
            }


            return invalidCount;
        }

        private static int RunBasicValidations(IEnumerable<RawRecord> records)
        {
            int invalidCount = 0;
            foreach (var r in records)
            {
                if (!r.Validate())
                {
                    ++invalidCount;
                    Log.Warn($"Line {r.OriginalLine:0000} is invalid. Reason: {r.InvalidReason}");
                }
            }

            return invalidCount;
        }

        private static IEnumerable<RawRecord> Parse(IEnumerable<string> allLines)
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
                
                var r = Parse(line);
                
                if (r == null)
                {
                    Log.Error($"Line {i+1} was ignored.");
                    continue;
                }

                r.OriginalLine = i + 1;
                yield return r;
            }
        }

        private static readonly Regex Splitter = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", RegexOptions.Compiled | RegexOptions.Singleline);

        private static RawRecord Parse(string line)
        {
            var f = Splitter.Split(line);

            if (f.Length < 10)
            {
                Log.Warn($"There are {f.Length} fields when the expected was at least 10");
                return null;
            }

            var r = new RawRecord
            {
                TeamName = f[0].Trim().Trim('"').ToUpperInvariant(),
                InGameName = f[1].Trim().Trim('"'),
                TeamLongName = f[2].Trim().Trim('"'),
                ParentClanTag = f[3].Trim().Trim('"').ToUpperInvariant(),
                ClanMembershipUrl = f[4].Trim().Trim('"'),
                TeamContactGamerTag = f[5].Trim().Trim('"'),
                TeamContactMail = f[6].Trim().Trim('"'),
                PreferredServer = f[7].Trim().Trim('"'),
                BackupServer = f[8].Trim().Trim('"')
            };

            if (string.IsNullOrWhiteSpace(f[9]))
            {
                return r;
            }

            r.OnLastSeasonTop32 = f[9].Split(new []{',', '"'}, StringSplitOptions.RemoveEmptyEntries);
            if ((r.OnLastSeasonTop32.Length == 1) && (r.OnLastSeasonTop32[0].Equals("N/A", StringComparison.InvariantCultureIgnoreCase)))
            {
                r.OnLastSeasonTop32 = new string[0];
            }

            for (int i = 0; i < r.OnLastSeasonTop32.Length; i++)
            {
                r.OnLastSeasonTop32[i] = r.OnLastSeasonTop32[i].Trim().Trim('"');
            }

            return r;
        }

    }
}
