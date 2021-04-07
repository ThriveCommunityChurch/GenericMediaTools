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
        /// Used to delete files after they've been successfully copied to the destination folder
        /// </summary>
        private static bool _deleteOnCopy;

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

            Log.Warning("Application Started");

            // read the other settings from appsettings.json
            _sourcePath = Configuration["SourcePath"];
            _destinationPath = Configuration["DestinationPath"];
            _fileExtension = Configuration["DesiredFileExtension"];

            _ = bool.TryParse(Configuration["DeleteOnCopy"], out bool deleteOnCopy);

            _deleteOnCopy = deleteOnCopy;
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
                Log.Information("Worker running at: {time}", DateTimeOffset.Now);

                HashSet<string> filesToCopy = new HashSet<string>(FileCopyService.CheckFiles(_sourcePath, _destinationPath, _fileExtension));

                filesToCopy.ExceptWith(_inProgressTransfers);
                _inProgressTransfers.UnionWith(filesToCopy);

                FileCopyService.CopyFiles(_destinationPath, filesToCopy);
                _inProgressTransfers.ExceptWith(filesToCopy);

                int copiedFiles = filesToCopy.Count;
                if (copiedFiles > 0)
                {
                    Log.Information($"Successfully copied {copiedFiles} files.");

                    if (_deleteOnCopy)
                    {
                        FileCopyService.DeleteOldFiles(filesToCopy);
                    }
                }
                else
                {
                    Log.Debug($"Nothing to transfer.");
                }

                // wait 30 seconds before running again
                await Task.Delay(30000, stoppingToken);
            }
        }
    }
}
