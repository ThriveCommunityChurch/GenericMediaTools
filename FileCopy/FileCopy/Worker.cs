using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileCopyServices;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.IO;

namespace FileCopy
{
    public class Worker : BackgroundService
    {
        public IConfigurationRoot Configuration { get; set; }

        private static string _sourcePath;
        private static string _destinationPath;
        private static string _fileExtension;

        /// <summary>
        /// File paths currently being transferred
        /// </summary>
        private static HashSet<string> _inProgressTransfers { get; set; } = new HashSet<string>();

        public Worker()
        {
            var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();

            var serilogSettings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(serilogSettings)
                .CreateLogger();

            Log.Logger = logger;

            // read the other settings from appsettings.json
            _sourcePath = Configuration["SourcePath"];
            _destinationPath = Configuration["DestinationPath"];
            _fileExtension = Configuration["DesiredFileExtension"];
        }

        /// <summary>
        /// Run this command operation loop based on this task delay
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Log.Debug("Worker running at: {time}", DateTimeOffset.Now);

                // wait 30 seconds before running again
                await Task.Delay(30000, stoppingToken);

                HashSet<string> filesToCopy = new HashSet<string>(FileCopyService.CheckFiles(_sourcePath, _destinationPath, _fileExtension));

                filesToCopy.ExceptWith(_inProgressTransfers);
                _inProgressTransfers.UnionWith(filesToCopy);

                FileCopyService.CopyFiles(_destinationPath, filesToCopy);
                _inProgressTransfers.ExceptWith(filesToCopy);

                int copiedFiles = filesToCopy.Count;
                if (copiedFiles > 0)
                {
                    Log.Information($"Successfully copied {copiedFiles} files.");
                }
                else
                {
                    Log.Debug($"Nothing to transfer.");
                }
            }
        }
    }
}
