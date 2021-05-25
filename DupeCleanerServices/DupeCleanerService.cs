using DupeCleaner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DupeCleanerServices
{
    public class DupeCleanerService
    {
        private readonly string _filePath;
        private readonly AppSettings _appSettings;
        private readonly HashSet<string> _fileExtensions;

        public DupeCleanerService(string filePath, AppSettings appSettings)
        {
            _filePath = filePath;
            _appSettings = appSettings;
            _fileExtensions = _appSettings.FileExtensionsToEnforce != null ? new HashSet<string>(_appSettings.FileExtensionsToEnforce, StringComparer.CurrentCultureIgnoreCase) : new HashSet<string>();
        }

        public void Run()
        {
            List<string> duplicateFilePaths = new();

            // in the event we need to iterate across all directories in this path we should have a seperate method and file path
            if (_appSettings.IncludeSubDirs)
            {
                duplicateFilePaths = CheckPathAndSubDirs();
            }
            else if (_appSettings.MergeImageExtensions)
            {
                duplicateFilePaths = CheckPath();
            }
            else if (!_appSettings.MergeImageExtensions && !_appSettings.MergeImageExtensions)
            {
                // Since its technically not possible to have the same file names in the same folder
                // we are going to assume that there's nothing wrong here with duplicates
                // so lets just return to the user that we're done
                return;
            }


            // if we found anty duplicates, we should remove the older version (if possible)
            // Otherwise just delete the first one, it doesn't really matter if they are duplicates
            if (duplicateFilePaths.Any())
            {
                // Delete files that we found
                DeleteStaleDuplicates(duplicateFilePaths);

                // Delete any empty directories as a result of our deletions
                DeleteEmptySubdirectories(_filePath);
            }

            // Log that we're done and how many dupes we cleaned. Tell the user to see the logfile for what files were deleted
        }

        /// <summary>
        /// Deletes all the files in the requested list of file paths
        /// </summary>
        /// <param name="duplicateFilePaths"></param>
        private void DeleteStaleDuplicates(IEnumerable<string> duplicateFilePaths)
        {
            Parallel.ForEach(duplicateFilePaths, filePath => 
            {
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    Console.WriteLine($"Unable to delete file at path: '{filePath}'.");
                }
            
            });
        }

        /// <summary>
        /// Checks only the requested file path for duplicate files and does not look deeper
        /// </summary>
        /// <returns></returns>
        private List<string> CheckPath()
        {
            string[] files = Directory.GetFiles(_filePath);

            return DetermineDupesNiave(files);
        }

        /// <summary>
        /// Checks the requested file path for duplicate files and all directories contained within this parent folder
        /// </summary>
        /// <returns></returns>
        private List<string> CheckPathAndSubDirs()
        {
            IEnumerable<string> allFiles = Directory.EnumerateFiles(_filePath, "*", SearchOption.AllDirectories);

            //if (_appSettings.EnforceImageExtensions)
            //{
            //    // TODO: Finish this
            //    throw new NotImplementedException();
            //}

            return DetermineDupesNiave(allFiles);
        }

        /// <summary>
        /// Recursively delete empty directories that contain no files
        /// </summary>
        /// <param name="parentDirectory"></param>
        private static void DeleteEmptySubdirectories(string parentDirectory)
        {
            long deletedFolders = 0;

            Parallel.ForEach(Directory.GetDirectories(parentDirectory), directory => {

                DeleteEmptySubdirectories(directory);

                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    deletedFolders++;
                    Directory.Delete(directory, false);
                }
                    
            });

            Console.WriteLine($"We cleaned out {deletedFolders} empty directories after deleting duplicates.");
        }

        /// <summary>
        /// The niave solution for determining duplicates in file paths, mainly used when ignoring file extensions
        /// </summary>
        /// <param name="filePaths"></param>
        /// <returns></returns>
        private List<string> DetermineDupesNiave(IEnumerable<string> filePaths)
        {
            List<string> duplicateFilePaths = new();

            // since technically it's not possible to have the same file name within the same folder,
            // we only really care about the same names across multiple folders
            Dictionary<string, List<string>> allFileNames = new(StringComparer.CurrentCultureIgnoreCase);
            Dictionary<string, List<string>> dupeFileNames = new();

            // First we need to figure out which of the file paths corrisponds to a dupe in the file names
            foreach (var filePath in filePaths)
            {
                // Split by \ characters assuming that in all cases we'll get \ rather than /
                string fileName = filePath.Split('\\').Last();
                string fileExtension = fileName.Split('.').Last();

                if (fileName.StartsWith("._") || !_fileExtensions.Contains(fileExtension))
                {
                    continue;
                }

                // Keep a record of the duplicates and keep a mapping of ALL the file names that match with a list of their corrisponding file locations
                if (allFileNames.ContainsKey(fileName))
                {
                    if (dupeFileNames.ContainsKey(fileName))
                    {
                        dupeFileNames[fileName].Add(filePath);
                    }
                    else
                    {
                        // assuming there is a dupe somewhere, we need to grab the first instance that we already looked at of this file
                        // since this list is ordered, it will always be the first thing in the list
                        dupeFileNames[fileName] = new List<string>() { allFileNames[fileName].First(), filePath };
                    }
                }
                else
                {
                    allFileNames[fileName] = new List<string>() { filePath };
                }
            }

            Console.WriteLine($"We found {dupeFileNames.Keys.Count} duplicate files.");

            // Now that we have the file names, we'll want to go to each instance of the file and figure out which ones have the same attributes
            foreach (var dupeFiles in dupeFileNames) 
            {
                DateTime newestFileTimeUTC = DateTime.MinValue;
                long? fileSize = null;
                string filePathToKeep = string.Empty;

                foreach (var dupeFilePath in dupeFiles.Value)
                {
                    var dupeFileName = dupeFiles.Key;
                    bool isMostRecentUpdate = false;
                    bool fileSizeMatches = false;

                    var createDate = Directory.GetCreationTimeUtc(dupeFilePath);
                    if (createDate > newestFileTimeUTC)
                    {
                        newestFileTimeUTC = createDate;
                        isMostRecentUpdate = true;
                    }

                    FileStream fileData = File.OpenRead(dupeFilePath);
                    long size = fileData.Length;

                    // this value hasn't been set yet
                    if (!fileSize.HasValue)
                    {
                        fileSize = size;
                    }
                    else if (size.Equals(fileSize))
                    {
                        fileSizeMatches = true;
                    }

                    // in the event this is the most recent value and there's been nothing set for the name, use this one
                    if (isMostRecentUpdate)
                    {
                        if (!string.IsNullOrEmpty(filePathToKeep) && fileSizeMatches)
                        {
                            duplicateFilePaths.Add(filePathToKeep);
                        }

                        filePathToKeep = dupeFilePath;
                    }
                    else if (fileSizeMatches)
                    {
                        // So now we know that this file is NOT the most recent one, and we not only have the same name
                        // but we also have the same file size, so one could assume then that these are the same file in seperate directories and we should likely delete this older one
                        duplicateFilePaths.Add(dupeFilePath);
                    }

                    // close the reader once completed so that we can delete the dupes using their paths
                    fileData.Dispose();
                 }

            }
            

            return duplicateFilePaths;
        }
    }
}