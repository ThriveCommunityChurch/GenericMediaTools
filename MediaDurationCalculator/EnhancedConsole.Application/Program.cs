using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using GenericMediaTools.MediaDurationCalculator.Infrastructure.Extensions;
using GenericMediaTools.MediaDurationCalculator.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GenericMediaTools.MediaDurationCalculator
{
    [ExcludeFromCodeCoverage]
    public class Program
    {
        private static IConfigurationRoot Configuration { get; set; }

        private static IServiceProvider ServiceProvider { get; set; }

        private static IIOService _ioService;

        public static async Task Main(string[] args)
        {
            const string consoleAppOperation = "What the console app does (ex: Data Uploader)";
            Stopwatch watch = Stopwatch.StartNew();
            int exitCode = 0;

            ConsoleExtensions.PrintStartMessage(consoleAppOperation);

            Configuration = ConsoleStartup.SetupConfiguration();
            ServiceProvider = ConsoleStartup.SetupDependencyInjection(Configuration);

            try
            {
                using (var scope = ServiceProvider.CreateScope())
                {
                    // Do console app stuff here!
                    _ioService = scope.ServiceProvider.GetRequiredService<IIOService>();

                    string directory = ServiceProvider.GetDirectoryFromConfig();

                    IEnumerable<FileInfo> files = _ioService.ListFilesInDirectory(directory);
                    await _ioService.CalculateVideoDuration(files);
                }
            }
            catch (Exception e)
            {
                ConsoleExtensions.PrintError($"\n {e} \n");
                exitCode = -1;
            }
            finally
            {
                watch.Stop();

                ConsoleExtensions.PrintExitMessage(consoleAppOperation, exitCode, watch);
            }
        }
    }
}