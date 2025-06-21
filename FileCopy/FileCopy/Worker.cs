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
        /// Minimum file size in MB to process (default: 100MB)
        /// </summary>
        private static int _minimumFileSizeMB;

        /// <summary>
        /// Tracks the last time configuration was loaded to detect changes
        /// </summary>
        private DateTime _lastConfigLoad = DateTime.MinValue;

        /// <summary>
        /// Expands environment variables only if the path contains them (e.g., %USERPROFILE%, %USERNAME%)
        /// This allows users to use either environment variables or absolute paths
        /// </summary>
        /// <param name="path">The path that may contain environment variables</param>
        /// <returns>The path with environment variables expanded if present, otherwise the original path</returns>
        private static string ExpandEnvironmentVariablesIfNeeded(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            // Only expand if the path contains environment variable syntax (% characters)
            if (path.Contains('%'))
            {
                return Environment.ExpandEnvironmentVariables(path);
            }

            return path;
        }

        /// <summary>
        /// File paths currently being transferred
        /// </summary>
        private static HashSet<string> _inProgressTransfers { get; set; } = new HashSet<string>();

        /// <summary>
        /// Tracks files that were locked and when they should be retried (file path -> next retry time)
        /// </summary>
        private static Dictionary<string, DateTime> _lockedFilesRetryQueue { get; set; } = new Dictionary<string, DateTime>();

        /// <summary>
        /// How long to wait before retrying a locked file (5 minutes)
        /// </summary>
        private static readonly TimeSpan _retryDelay = TimeSpan.FromMinutes(5);

        public Worker()
        {
            try
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

                Log.Information("FileCopy service initializing...");

                // read the other settings from appsettings.json
                _sourcePath = ExpandEnvironmentVariablesIfNeeded(Configuration["SourcePath"]);
                _destinationPath = Configuration["DestinationPath"]; // Keep destination path as-is (no env var expansion)
                _fileExtension = Configuration["DesiredFileExtension"];

                // Read minimum file size (default to 100MB if not specified)
                if (!int.TryParse(Configuration["MinimumFileSizeMB"], out _minimumFileSizeMB))
                {
                    _minimumFileSizeMB = 100; // Default value
                }

                // Validate required configuration
                if (string.IsNullOrWhiteSpace(_sourcePath))
                {
                    throw new InvalidOperationException("SourcePath configuration is required but not provided.");
                }
                if (string.IsNullOrWhiteSpace(_destinationPath))
                {
                    throw new InvalidOperationException("DestinationPath configuration is required but not provided.");
                }

                _ = bool.TryParse(Configuration["DeleteOnCopy"], out bool deleteOnCopy);
                _deleteOnCopy = deleteOnCopy;

                Log.Information("Configuration loaded successfully:");
                Log.Information("  Source Path: {SourcePath}", _sourcePath);
                Log.Information("  Destination Path: {DestinationPath}", _destinationPath);
                Log.Information("  File Extension: {FileExtension}", string.IsNullOrEmpty(_fileExtension) ? "All files" : _fileExtension);
                Log.Information("  Delete On Copy: {DeleteOnCopy}", _deleteOnCopy);
                Log.Information("  Minimum File Size: {MinimumFileSizeMB}MB", _minimumFileSizeMB);

                _lastConfigLoad = DateTime.Now;
            }
            catch (Exception ex)
            {
                // If logging isn't set up yet, we need to handle this differently
                var fallbackLogger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .CreateLogger();

                fallbackLogger.Fatal(ex, "Failed to initialize FileCopy service configuration");
                throw;
            }
        }

        /// <summary>
        /// Reloads configuration from appsettings.json and updates service settings
        /// </summary>
        private bool ReloadConfiguration()
        {
            try
            {
                // Force configuration reload
                Configuration.Reload();

                var newSourcePath = ExpandEnvironmentVariablesIfNeeded(Configuration["SourcePath"]);
                var newDestinationPath = Configuration["DestinationPath"]; // Keep destination path as-is (no env var expansion)
                var newFileExtension = Configuration["DesiredFileExtension"];
                _ = bool.TryParse(Configuration["DeleteOnCopy"], out bool newDeleteOnCopy);

                // Read minimum file size (default to 100MB if not specified)
                if (!int.TryParse(Configuration["MinimumFileSizeMB"], out int newMinimumFileSizeMB))
                {
                    newMinimumFileSizeMB = 100; // Default value
                }

                // Check if any settings changed
                bool configChanged = false;
                if (_sourcePath != newSourcePath)
                {
                    Log.Information("Source path changed from '{OldPath}' to '{NewPath}'", _sourcePath, newSourcePath);
                    _sourcePath = newSourcePath;
                    configChanged = true;
                }

                if (_destinationPath != newDestinationPath)
                {
                    Log.Information("Destination path changed from '{OldPath}' to '{NewPath}'", _destinationPath, newDestinationPath);
                    _destinationPath = newDestinationPath;
                    configChanged = true;
                }

                if (_fileExtension != newFileExtension)
                {
                    Log.Information("File extension changed from '{OldExt}' to '{NewExt}'", _fileExtension, newFileExtension);
                    _fileExtension = newFileExtension;
                    configChanged = true;
                }

                if (_deleteOnCopy != newDeleteOnCopy)
                {
                    Log.Information("Delete on copy changed from '{OldValue}' to '{NewValue}'", _deleteOnCopy, newDeleteOnCopy);
                    _deleteOnCopy = newDeleteOnCopy;
                    configChanged = true;
                }

                if (_minimumFileSizeMB != newMinimumFileSizeMB)
                {
                    Log.Information("Minimum file size changed from '{OldValue}MB' to '{NewValue}MB'", _minimumFileSizeMB, newMinimumFileSizeMB);
                    _minimumFileSizeMB = newMinimumFileSizeMB;
                    configChanged = true;
                }

                if (configChanged)
                {
                    Log.Information("Configuration reloaded successfully with changes");
                    _lastConfigLoad = DateTime.Now;

                    // Clear retry queue since paths may have changed
                    if (_lockedFilesRetryQueue.Any())
                    {
                        Log.Information("Clearing retry queue due to configuration changes");
                        _lockedFilesRetryQueue.Clear();
                    }
                }

                return configChanged;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to reload configuration");
                return false;
            }
        }

        /// <summary>
        /// Run this command operation loop based on this task delay
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("FileCopy service started. Monitoring source: {SourcePath}, Destination: {DestinationPath}, Extension: {Extension}",
                _sourcePath, _destinationPath, _fileExtension);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Log.Debug("Worker cycle starting at: {time}", DateTimeOffset.Now);

                    // Check for configuration changes every 30 seconds
                    if (DateTime.Now.Subtract(_lastConfigLoad).TotalSeconds > 30)
                    {
                        var configFile = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                        if (File.Exists(configFile))
                        {
                            var lastWriteTime = File.GetLastWriteTime(configFile);
                            if (lastWriteTime > _lastConfigLoad)
                            {
                                Log.Information("Configuration file changed, reloading...");
                                ReloadConfiguration();
                            }
                        }
                    }

                    // Validate paths exist
                    if (!Directory.Exists(_sourcePath))
                    {
                        Log.Warning("Source directory does not exist: {SourcePath}. Creating it...", _sourcePath);
                        try
                        {
                            Directory.CreateDirectory(_sourcePath);
                            Log.Information("Created source directory: {SourcePath}", _sourcePath);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to create source directory: {SourcePath}", _sourcePath);
                            await Task.Delay(60000, stoppingToken); // Wait before retrying
                            continue;
                        }
                    }

                    // Check for files ready to retry from the locked files queue
                    var filesToRetry = new HashSet<string>();
                    var currentTime = DateTime.Now;
                    var expiredRetries = _lockedFilesRetryQueue.Where(kvp => kvp.Value <= currentTime).ToList();

                    if (expiredRetries.Any())
                    {
                        Log.Information("Found {RetryCount} file(s) ready for retry", expiredRetries.Count);
                    }

                    foreach (var expiredRetry in expiredRetries)
                    {
                        filesToRetry.Add(expiredRetry.Key);
                        _lockedFilesRetryQueue.Remove(expiredRetry.Key);
                        Log.Information($"Retrying previously locked file: {Path.GetFileName(expiredRetry.Key)}");
                    }

                // Check if there are any files to process (either new files or retry files)
                HashSet<string> newFilesToCopy = new HashSet<string>(FileCopyService.CheckFiles(_sourcePath, _destinationPath, _fileExtension, _minimumFileSizeMB));

                if (!newFilesToCopy.Any() && !filesToRetry.Any())
                {
                    Log.Debug($"Nothing to transfer.");
                }
                else
                {
                    // Use robocopy for the bulk operation if we're moving files (deleteOnCopy = true)
                    HashSet<string> lockedFiles = new HashSet<string>();

                    if (_deleteOnCopy)
                    {
                        // Use robocopy-style native C# implementation to move files
                        lockedFiles = FileCopyService.RobocopyStyleMoveFiles(_sourcePath, _destinationPath, _fileExtension, _minimumFileSizeMB);
                    }
                    else
                    {
                        // For copy operations, fall back to individual file operations
                        // since robocopy /mov parameter moves files, not copies them
                        HashSet<string> allFilesToCopy = new HashSet<string>(newFilesToCopy);
                        allFilesToCopy.UnionWith(filesToRetry);

                        allFilesToCopy.ExceptWith(_inProgressTransfers);
                        _inProgressTransfers.UnionWith(allFilesToCopy);

                        lockedFiles = FileCopyService.CopyFiles(_destinationPath, allFilesToCopy);

                        // Reset this list for the next iteration
                        _inProgressTransfers.ExceptWith(allFilesToCopy);
                    }

                    // Add locked files to retry queue
                    foreach (var lockedFile in lockedFiles)
                    {
                        var retryTime = currentTime.Add(_retryDelay);
                        _lockedFilesRetryQueue[lockedFile] = retryTime;
                        Log.Information($"File '{Path.GetFileName(lockedFile)}' is locked. Will retry at {retryTime:HH:mm:ss}");
                    }

                    // Log results
                    int totalFilesAttempted = newFilesToCopy.Count + filesToRetry.Count;
                    int successfulFiles = totalFilesAttempted - lockedFiles.Count;
                    if (successfulFiles > 0)
                    {
                        Log.Information($"Successfully {(_deleteOnCopy ? "moved" : "copied")} {successfulFiles} file(s).");
                    }
                    if (lockedFiles.Count > 0)
                    {
                        Log.Information($"{lockedFiles.Count} file(s) were locked and will be retried later.");
                    }
                }

                }
                catch (OperationCanceledException)
                {
                    Log.Information("FileCopy service is shutting down.");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unexpected error in FileCopy service worker cycle");
                }

                // wait 1 minute before running again
                await Task.Delay(60000, stoppingToken);
            }

            Log.Information("FileCopy service stopped.");
        }
    }
}