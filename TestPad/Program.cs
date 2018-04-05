using System;
using System.IO;
using System.Text;
using log4net;
using log4net.Config;
using Newtonsoft.Json.Linq;

namespace Negri.Wot
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        static int Main(string[] args)
        {
            try
            {
                if (args.Length < 0)
                {
                    Log.Error("No argumments.");
                    return 2;
                }

                if (args.Length >= 2 && args[0].EqualsCiAi("parseDivision"))
                {
                    return ParseDivisionFile(args[1]);
                }

                Log.Error("Not a valid command.");
                return 3;
            }
            catch (Exception ex)
            {
                Log.Fatal("Main", ex);
                return 1;
            }
        }

        /// <summary>
        /// Parses the json of a division and writes a file on clans and groups
        /// </summary>
        /// <remarks>
        /// You can find the original json downloading from (for example)
        /// Euro: https://dtmwra1jsgyb0.cloudfront.net/stages/5abf379a7049ea03465cf5e3?extend%5Bgroups%5D%5Bteams%5D=true
        /// East: https://dtmwra1jsgyb0.cloudfront.net/stages/5abf37d5b820c003525d1a95?extend%5Bgroups%5D%5Bteams%5D=true
        /// West: https://dtmwra1jsgyb0.cloudfront.net/stages/5abf37ffb946620355bcd989?extend%5Bgroups%5D%5Bteams%5D=true
        /// </remarks>
        private static int ParseDivisionFile(string dividionFile)
        {
            var s = File.ReadAllText(dividionFile, Encoding.UTF8);
            var all = JArray.Parse(s)[0];

            var sb = new StringBuilder();
            sb.AppendLine("ClanTag,Division,Group");

            string fullName = (string) all["name"];

            Log.Info($"File is from {fullName}");

            string division = fullName;
            if (fullName.ToLowerInvariant().Contains("west"))
            {
                division = "WEST";
            }
            else if (fullName.ToLowerInvariant().Contains("east"))
            {
                division = "EAST";
            }
            else if (fullName.ToLowerInvariant().Contains("euro"))
            {
                division = "EURO";
            }

            var groups = (JArray)all["groups"];
            foreach (var g in groups)
            {
                var groupName = (string) g["name"];
                Log.Info($"Found group {groupName}...");

                var teams = (JArray) g["teams"];

                foreach (var t in teams)
                {
                    var teamName = (string) t["name"];
                    Log.Info($"Found team {teamName} on group {groupName}...");

                    sb.AppendLine($"{teamName},{division},{groupName}");
                }
            }

            var outFile = Path.ChangeExtension(dividionFile, "csv");
            File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);

            return 0;
        }
    }
}
