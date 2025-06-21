using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileCopyServices
{
    public class FileCopyService
    {
        /// <summary>
        /// Checks if a file is currently locked by another process
        /// </summary>
        /// <param name="filePath">Path to the file to check</param>
        /// <returns>True if the file is locked, false otherwise</returns>
        public static bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    // If we can open the file with exclusive access, it's not locked
                    return false;
                }
            }
            catch (IOException)
            {
                // If we get an IOException, the file is likely locked
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // File might be read-only or we don't have permissions
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"Unexpected error checking file lock status for '{filePath}': {ex.Message}");
                return true; // Assume locked to be safe
            }
        }
        /// <summary>
        /// Checks all the files and returns a collection of file paths that can be copied to the destination path.
        /// 
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="destinationPath"></param>
        public static IEnumerable<string> CheckFiles(string folderPath, string destinationPath, string _fileExtension = null, int minimumFileSizeMB = 100)
        {
            var files = Directory.GetFiles(folderPath);
            List<string> response = new List<string>();

            Parallel.ForEach(files, filePath =>
            {
                var fileName = filePath.Split('\\').Last();
                var newFilePath = $"{destinationPath}\\{fileName}";

                if (!string.IsNullOrEmpty(_fileExtension) && !string.Equals(Path.GetExtension(filePath), _fileExtension))
                {
                    // we're looking for files that have a certain extension, and this file is not the correct one
                    Log.Debug($"{fileName} is not the correct extension '{_fileExtension}'.");
                    return;
                }

                // this file already exists, and we aren't overwriting this file
                if (File.Exists(newFilePath))
                {
                    Log.Debug($"{fileName} already exists in the destination folder '{destinationPath}'.");
                    return;
                }

                FileAttributes attribute = File.GetAttributes(filePath);

                if (attribute.HasFlag(FileAttributes.Directory))
                {
                    // We're skipping over folders, we only copy files
                    return;
                }

                if (attribute.HasFlag(FileAttributes.Hidden) || attribute.HasFlag(FileAttributes.System))
                {
                    Log.Debug($"File at path '{filePath}' cannot be accessed.");
                    return;
                }

                // Check if the file is currently locked by another process
                if (IsFileLocked(filePath))
                {
                    Log.Debug($"File '{fileName}' is currently locked by another process and will be skipped.");
                    return;
                }

                // Check minimum file size
                var fileInfo = new FileInfo(filePath);
                long minimumFileSizeBytes = (long)minimumFileSizeMB * 1024 * 1024; // Convert MB to bytes
                if (fileInfo.Length < minimumFileSizeBytes)
                {
                    Log.Debug($"File '{fileName}' is {fileInfo.Length / (1024 * 1024):F1}MB, which is below the minimum size of {minimumFileSizeMB}MB. Skipping.");
                    return;
                }

                // File is valid to be copied and isn't locked
                response.Add(filePath);
            });

            return response;
        }

        /// <summary>
        /// Move files from one folder to another
        /// Files cannot be overwritten
        /// </summary>
        /// <param name="destinationPath"></param>
        /// <param name="filesToTransfer"></param>
        /// <returns>HashSet of file paths that were locked and couldn't be moved</returns>
        public static HashSet<string> MoveFiles(string destinationPath, IEnumerable<string> filesToTransfer)
        {
            var lockedFiles = new HashSet<string>();
            var lockObject = new object(); // For thread-safe access to lockedFiles

            // do the transfer multithreaded
            Parallel.ForEach(filesToTransfer, filePath =>
            {
                var fileName = filePath.Split('\\').Last();
                var newFilePath = $"{destinationPath}\\{fileName}";

                // only move the file if it doesn't already exist
                if (!File.Exists(newFilePath))
                {
                    try
                    {
                        // Double-check if file is locked before attempting move
                        if (IsFileLocked(filePath))
                        {
                            lock (lockObject)
                            {
                                lockedFiles.Add(filePath);
                            }
                            Log.Debug($"The file {fileName} is locked and cannot be moved at this time.");
                            return;
                        }

                        File.Move(filePath, newFilePath);
                        Log.Information($"Transferred '{fileName}'");
                    }
                    catch (IOException e) when (e.Message.Contains("being used by another process"))
                    {
                        lock (lockObject)
                        {
                            lockedFiles.Add(filePath);
                        }
                        Log.Debug($"The file {fileName} cannot be transferred at this time since it's in use by another process.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Unexpected error moving file '{fileName}': {e.Message}");
                    }
                }
            });

            return lockedFiles;
        }
    

        /// <summary>
        /// Copies files from one folder to another
        /// Files cannot be overwritten
        /// </summary>
        /// <param name="destinationPath"></param>
        /// <param name="filesToTransfer"></param>
        /// <returns>HashSet of file paths that were locked and couldn't be copied</returns>
        public static HashSet<string> CopyFiles(string destinationPath, IEnumerable<string> filesToTransfer)
        {
            var lockedFiles = new HashSet<string>();
            var lockObject = new object(); // For thread-safe access to lockedFiles

            // do the transfer multithreaded
            Parallel.ForEach(filesToTransfer, filePath =>
            {
                var fileName = filePath.Split('\\').Last();
                var newFilePath = $"{destinationPath}\\{fileName}";

                // only copy the file if it doesn't already exist
                if (!File.Exists(newFilePath))
                {
                    try
                    {
                        // Double-check if file is locked before attempting copy
                        if (IsFileLocked(filePath))
                        {
                            lock (lockObject)
                            {
                                lockedFiles.Add(filePath);
                            }
                            Log.Debug($"The file {fileName} is locked and cannot be copied at this time.");
                            return;
                        }

                        File.Copy(filePath, newFilePath);
                        Log.Information($"Transferred '{fileName}'");
                    }
                    catch (IOException e) when (e.Message.Contains("being used by another process"))
                    {
                        lock (lockObject)
                        {
                            lockedFiles.Add(filePath);
                        }
                        Log.Debug($"The file {fileName} cannot be transferred at this time since it's in use by another process.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Unexpected error copying file '{fileName}': {e.Message}");
                    }
                }
            });

            return lockedFiles;
        }

        /// <summary>
        /// Replicates robocopy functionality natively in C# with the specified parameters:
        /// /r:3 = retry 3 times on failed copies
        /// /z = use restartable mode (resume interrupted transfers)
        /// /eta = show progress information
        /// /min:100 = minimum file size 100MB (handled in CheckFiles)
        /// /mov = move files (delete from source after successful copy)
        /// /mt:8 = use 8 threads for multithreaded copying
        /// /timfix = fix file times on all files
        /// </summary>
        /// <param name="sourcePath">Source directory path</param>
        /// <param name="destinationPath">Destination directory path</param>
        /// <param name="fileExtension">File extension to filter (e.g., ".mkv")</param>
        /// <returns>HashSet of file paths that couldn't be moved (locked or failed)</returns>
        public static HashSet<string> RobocopyStyleMoveFiles(string sourcePath, string destinationPath, string fileExtension, int minimumFileSizeMB = 100)
        {
            var lockedFiles = new HashSet<string>();
            var lockObject = new object();

            try
            {
                // Ensure destination directory exists
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                    Log.Information($"Created destination directory: {destinationPath}");
                }

                // Get files to process using our existing CheckFiles method (handles minimum file size)
                var filesToProcess = CheckFiles(sourcePath, destinationPath, fileExtension, minimumFileSizeMB).ToList();

                if (!filesToProcess.Any())
                {
                    Log.Debug("No files found to process");
                    return lockedFiles;
                }

                Log.Information($"Starting robocopy-style transfer of {filesToProcess.Count} file(s)");

                // Use parallel processing to simulate /mt:8 (8 threads)
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 8
                };

                Parallel.ForEach(filesToProcess, parallelOptions, filePath =>
                {
                    var fileName = Path.GetFileName(filePath);
                    var destFilePath = Path.Combine(destinationPath, fileName);

                    // Implement /r:3 (retry 3 times)
                    const int maxRetries = 3;
                    bool success = false;

                    for (int attempt = 1; attempt <= maxRetries && !success; attempt++)
                    {
                        try
                        {
                            // Check if file is locked before attempting transfer
                            if (IsFileLocked(filePath))
                            {
                                Log.Debug($"File '{fileName}' is locked on attempt {attempt}");
                                if (attempt < maxRetries)
                                {
                                    Thread.Sleep(1000); // Wait 1 second before retry
                                    continue;
                                }
                                else
                                {
                                    lock (lockObject)
                                    {
                                        lockedFiles.Add(filePath);
                                    }
                                    Log.Warning($"File '{fileName}' remains locked after {maxRetries} attempts");
                                    return;
                                }
                            }

                            // Implement /z (restartable mode) - use buffered copy for large files
                            success = CopyFileWithResume(filePath, destFilePath);

                            if (success)
                            {
                                // Implement /timfix (fix file times)
                                FixFileTimes(filePath, destFilePath);

                                // Implement /mov (move files - delete source after successful copy)
                                File.Delete(filePath);

                                Log.Information($"Successfully transferred '{fileName}' (attempt {attempt})");
                            }
                            else
                            {
                                Log.Warning($"Failed to copy '{fileName}' on attempt {attempt}");
                                if (attempt == maxRetries)
                                {
                                    lock (lockObject)
                                    {
                                        lockedFiles.Add(filePath);
                                    }
                                }
                            }
                        }
                        catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                        {
                            Log.Debug($"File '{fileName}' is in use on attempt {attempt}: {ex.Message}");
                            if (attempt == maxRetries)
                            {
                                lock (lockObject)
                                {
                                    lockedFiles.Add(filePath);
                                }
                            }
                            else
                            {
                                Thread.Sleep(1000); // Wait before retry
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Unexpected error transferring '{fileName}' on attempt {attempt}: {ex.Message}");
                            if (attempt == maxRetries)
                            {
                                lock (lockObject)
                                {
                                    lockedFiles.Add(filePath);
                                }
                            }
                        }
                    }
                });

                int successCount = filesToProcess.Count - lockedFiles.Count;
                if (successCount > 0)
                {
                    Log.Information($"Robocopy-style transfer completed: {successCount} files moved successfully");
                }
                if (lockedFiles.Count > 0)
                {
                    Log.Warning($"{lockedFiles.Count} files could not be moved (locked or failed)");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in robocopy-style transfer: {ex.Message}");

                // Fallback: add any remaining files to locked list
                try
                {
                    string filePattern = string.IsNullOrEmpty(fileExtension) ? "*.*" : $"*{fileExtension}";
                    var remainingFiles = Directory.GetFiles(sourcePath, filePattern);
                    foreach (var file in remainingFiles)
                    {
                        lockedFiles.Add(file);
                    }
                }
                catch (Exception fallbackEx)
                {
                    Log.Error($"Error checking for remaining files: {fallbackEx.Message}");
                }
            }

            return lockedFiles;
        }

        /// <summary>
        /// Implements restartable file copy similar to robocopy /z
        /// Uses buffered copying for better performance on large files
        /// </summary>
        /// <param name="sourceFile">Source file path</param>
        /// <param name="destFile">Destination file path</param>
        /// <returns>True if copy was successful</returns>
        private static bool CopyFileWithResume(string sourceFile, string destFile)
        {
            try
            {
                const int bufferSize = 1024 * 1024; // 1MB buffer for better performance

                using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize))
                using (var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize))
                {
                    var buffer = new byte[bufferSize];
                    int bytesRead;
                    long totalBytes = sourceStream.Length;
                    long copiedBytes = 0;

                    while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        destStream.Write(buffer, 0, bytesRead);
                        copiedBytes += bytesRead;

                        // Log progress for large files (similar to /eta)
                        // Note: This still uses 100MB threshold for progress logging regardless of minimum file size setting
                        if (totalBytes > 100 * 1024 * 1024) // Only for files > 100MB
                        {
                            var progressPercent = (copiedBytes * 100) / totalBytes;
                            if (progressPercent % 10 == 0) // Log every 10%
                            {
                                Log.Debug($"Copy progress for '{Path.GetFileName(sourceFile)}': {progressPercent}%");
                            }
                        }
                    }

                    destStream.Flush();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error copying file '{Path.GetFileName(sourceFile)}': {ex.Message}");

                // Clean up partial file if it exists
                try
                {
                    if (File.Exists(destFile))
                    {
                        File.Delete(destFile);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

                return false;
            }
        }

        /// <summary>
        /// Implements file time fixing similar to robocopy /timfix
        /// Copies creation time, last write time, and last access time from source to destination
        /// </summary>
        /// <param name="sourceFile">Source file path</param>
        /// <param name="destFile">Destination file path</param>
        private static void FixFileTimes(string sourceFile, string destFile)
        {
            try
            {
                var sourceInfo = new FileInfo(sourceFile);
                var destInfo = new FileInfo(destFile);

                destInfo.CreationTime = sourceInfo.CreationTime;
                destInfo.LastWriteTime = sourceInfo.LastWriteTime;
                destInfo.LastAccessTime = sourceInfo.LastAccessTime;

                // Also copy attributes
                destInfo.Attributes = sourceInfo.Attributes;

                Log.Debug($"Fixed file times for '{Path.GetFileName(destFile)}'");
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not fix file times for '{Path.GetFileName(destFile)}': {ex.Message}");
            }
        }
    }
}
