using System;
using System.IO;
using System.Reflection;
using System.Text;
using log4net;

namespace Negri.Wot.Wcl
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
                    Log.Warn("Try\r\nWCLUtility Validate Battlefy_master_file.csv");
                    return 2;
                }

                if (!args[0].Equals("Validate", StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.Error("Invalid command!");
                    Log.Warn("Try\r\nWCLUtility Validate Battlefy_master_file.csv");
                    return 3;
                }

                string originalFile = args[1];
                if (!File.Exists(originalFile))
                {
                    Log.Error($"Could not find the file '{originalFile}'");
                    return 4;
                }

                bool calculatePerformance = false;
                if ((args.Length >= 3) && (args[2]).Equals("performance", StringComparison.InvariantCultureIgnoreCase))
                {
                    calculatePerformance = true;
                }

                WclValidator validator = new WclValidator()
                {
                    AppId = GetApplicationId(),
                    CalculatePerformance = calculatePerformance
                };
                if (validator.Run(originalFile) == false)
                {
                    Log.Fatal(validator.LastFatalError);
                    return 10;
                }

                Log.Info("Bye!");
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal("Unhandled Exception", ex);
                return 1;
            }
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

        
    }
}