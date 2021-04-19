using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileCopyServices
{
    public class FileCopyService
    {
        /// <summary>
        /// Checks all the files and returns a collection of file paths that can be copied to the destination path.
        /// 
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="destinationPath"></param>
        public static IEnumerable<string> CheckFiles(string folderPath, string destinationPath, string _fileExtension = null)
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

                // assuming this file is valid to be copied and isn't going to be altered during the transfer, 
                // then we can add it to the response here and copy it over
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
        public static void MoveFiles(string destinationPath, IEnumerable<string> filesToTransfer)
        {
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
                        File.Move(filePath, newFilePath);

                        Log.Information($"Tranferred '{fileName}'");
                    }
                    catch (Exception e)
                    {
                        if (e.Message.Contains("being used by another process"))
                        {
                            Log.Debug($"The file {fileName} cannot be transferred at this time since it's in use by another process.");
                        }

                        return;
                    }
                }
            });
        }
    

        /// <summary>
        /// Copies files from one folder to another
        /// Files cannot be overwritten
        /// </summary>
        /// <param name="destinationPath"></param>
        /// <param name="filesToTransfer"></param>
        public static void CopyFiles(string destinationPath, IEnumerable<string> filesToTransfer)
        {
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
                        File.Copy(filePath, newFilePath);

                        Log.Information($"Tranferred '{fileName}'");
                    }
                    catch (Exception e)
                    {
                        if (e.Message.Contains("being used by another process"))
                        {
                            Log.Debug($"The file {fileName} cannot be transferred at this time since it's in use by another process.");
                        }

                        return;
                    }
                }
            });

        }
    }
}
