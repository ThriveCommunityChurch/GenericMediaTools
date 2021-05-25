using CommandLine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DupeCleaner
{
    internal class ProgramBase
    {
        internal static Options _options = new();
        internal static AppSettings _appSettings;

        /// <summary>
        /// Reads all the configs from the appsettings.json file
        /// </summary>
        internal static void ReadAppSettings()
        {
            using StreamReader file = new("appsettings.json");
            var fileAsString = file.ReadToEnd();

            _appSettings = JsonConvert.DeserializeObject<AppSettings>(fileAsString);
        }

        /// <summary>
        /// Read the CLI args in and parse as Options object
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        internal static void ParseArguments(string[] args)
        {
            try
            {
                var result = Parser.Default.ParseArguments<Options>(args).MapResult((opts) => MapOptions(opts), //in case parser sucess
                             errs => HandleParseError(errs)); //in  case parser fail

                if (result != 1)
                {
                    throw new Exception("One or more Options failed to parse. Check log messages above for more info.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw;
            }
        }

        private static int MapOptions(Options opts)
        {
            _options = opts;
            return 1;
        }

        private static int HandleParseError(IEnumerable<Error> errs)
        {
            var result = -2;
            Console.WriteLine("errors {0}", errs.Count());

            if (errs.Any(x => x is HelpRequestedError || x is VersionRequestedError))
            {
                result = -1;
            }

            Console.WriteLine("Exit code {0}", result);
            return result;
        }
    }
}
