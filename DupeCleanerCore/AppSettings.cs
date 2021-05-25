using System.Collections.Generic;

namespace DupeCleaner
{
    public class AppSettings
    {
        /// <summary>
        /// Whether or not to enforce uniqueness across similar image extensions (specifically .jpg/.jpeg)
        /// </summary>
        public bool MergeImageExtensions { get; set; }

        /// <summary>
        /// Whether or not to search for duplicates in all sub directories of the requested path
        /// </summary>
        public bool IncludeSubDirs { get; set; }

        /// <summary>
        /// A collection containing the file names to enforce. This can be useful when looking very deep in file paths and avoids deleting certain file extensions that might be required for certain applications.
        /// Leave this blank to enforce across all file extensions
        /// </summary>
        public IEnumerable<string> FileExtensionsToEnforce { get; set; }
    }
}