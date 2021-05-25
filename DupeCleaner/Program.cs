using DupeCleanerServices;
using System;

namespace DupeCleaner
{
    class Program: ProgramBase
    {
        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                ReadAppSettings();
                ParseArguments(args);

                if (string.IsNullOrEmpty(_options.Path))
                {
                    throw new ArgumentException($"Required argument: {nameof(Options.Path)}");
                }
            }
            else
            {
                throw new ArgumentException($"Required arguments: [{nameof(Options.Path)}]");
            }

            // start execution
            DupeCleanerService cleanerService = new(_options.Path, _appSettings);
            cleanerService.Run();
        }
    }
}