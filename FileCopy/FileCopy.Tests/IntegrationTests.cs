using FileCopyServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileCopy.Tests
{
    [TestClass]
    public class IntegrationTests
    {
        private string _testSourcePath = null!;
        private string _testDestinationPath = null!;
        private ILogger _logger = null!;

        [TestInitialize]
        public void Setup()
        {
            // Create temporary directories for testing
            _testSourcePath = Path.Combine(Path.GetTempPath(), "IntegrationTest_Source", Guid.NewGuid().ToString());
            _testDestinationPath = Path.Combine(Path.GetTempPath(), "IntegrationTest_Dest", Guid.NewGuid().ToString());
            
            Directory.CreateDirectory(_testSourcePath);
            Directory.CreateDirectory(_testDestinationPath);

            // Setup logger
            _logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
            Log.Logger = _logger;
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test directories
            CleanupDirectory(_testSourcePath);
            CleanupDirectory(_testDestinationPath);
        }

        private void CleanupDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        private byte[] CreateLargeFileContent(int sizeInMB)
        {
            return new byte[sizeInMB * 1024 * 1024];
        }

        [TestMethod]
        public void EndToEnd_SingleValidFile_MovesSuccessfully()
        {
            // Arrange
            var fileName = "recording.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            var destFile = Path.Combine(_testDestinationPath, fileName);
            
            // Create a file larger than 100MB
            var fileContent = CreateLargeFileContent(101);
            File.WriteAllBytes(sourceFile, fileContent);

            // Act
            var filesToCheck = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");
            var lockedFiles = FileCopyService.MoveFiles(_testDestinationPath, filesToCheck);

            // Assert
            Assert.AreEqual(1, filesToCheck.Count());
            Assert.AreEqual(0, lockedFiles.Count);
            Assert.IsFalse(File.Exists(sourceFile), "Source file should be moved (deleted)");
            Assert.IsTrue(File.Exists(destFile), "Destination file should exist");
            
            var destContent = File.ReadAllBytes(destFile);
            Assert.AreEqual(fileContent.Length, destContent.Length, "File content should match");
        }

        [TestMethod]
        public void EndToEnd_MultipleValidFiles_MovesAllSuccessfully()
        {
            // Arrange
            var fileNames = new[] { "recording1.mkv", "recording2.mkv", "recording3.mkv" };
            var fileContent = CreateLargeFileContent(101);

            foreach (var fileName in fileNames)
            {
                var sourceFile = Path.Combine(_testSourcePath, fileName);
                File.WriteAllBytes(sourceFile, fileContent);
            }

            // Act
            var filesToCheck = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");
            var lockedFiles = FileCopyService.MoveFiles(_testDestinationPath, filesToCheck);

            // Assert
            Assert.AreEqual(3, filesToCheck.Count());
            Assert.AreEqual(0, lockedFiles.Count);

            foreach (var fileName in fileNames)
            {
                var sourceFile = Path.Combine(_testSourcePath, fileName);
                var destFile = Path.Combine(_testDestinationPath, fileName);
                
                Assert.IsFalse(File.Exists(sourceFile), $"Source file {fileName} should be moved");
                Assert.IsTrue(File.Exists(destFile), $"Destination file {fileName} should exist");
            }
        }

        [TestMethod]
        public void EndToEnd_MixedFileSizes_OnlyMovesLargeFiles()
        {
            // Arrange
            var largeFile = Path.Combine(_testSourcePath, "large.mkv");
            var smallFile = Path.Combine(_testSourcePath, "small.mkv");
            
            File.WriteAllBytes(largeFile, CreateLargeFileContent(101)); // 101MB
            File.WriteAllText(smallFile, "small content"); // Much smaller than 100MB

            // Act
            var filesToCheck = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");
            var lockedFiles = FileCopyService.MoveFiles(_testDestinationPath, filesToCheck);

            // Assert
            Assert.AreEqual(1, filesToCheck.Count(), "Only large file should be selected");
            Assert.AreEqual(0, lockedFiles.Count);
            
            Assert.IsFalse(File.Exists(largeFile), "Large file should be moved");
            Assert.IsTrue(File.Exists(smallFile), "Small file should remain in source");
            Assert.IsTrue(File.Exists(Path.Combine(_testDestinationPath, "large.mkv")), "Large file should exist in destination");
        }

        [TestMethod]
        public void EndToEnd_MixedFileTypes_OnlyMovesCorrectExtension()
        {
            // Arrange
            var mkvFile = Path.Combine(_testSourcePath, "video.mkv");
            var mp4File = Path.Combine(_testSourcePath, "video.mp4");
            var fileContent = CreateLargeFileContent(101);
            
            File.WriteAllBytes(mkvFile, fileContent);
            File.WriteAllBytes(mp4File, fileContent);

            // Act
            var filesToCheck = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");
            var lockedFiles = FileCopyService.MoveFiles(_testDestinationPath, filesToCheck);

            // Assert
            Assert.AreEqual(1, filesToCheck.Count(), "Only .mkv file should be selected");
            Assert.AreEqual(0, lockedFiles.Count);
            
            Assert.IsFalse(File.Exists(mkvFile), "MKV file should be moved");
            Assert.IsTrue(File.Exists(mp4File), "MP4 file should remain in source");
            Assert.IsTrue(File.Exists(Path.Combine(_testDestinationPath, "video.mkv")), "MKV file should exist in destination");
        }

        [TestMethod]
        public void EndToEnd_FileAlreadyExists_SkipsFile()
        {
            // Arrange
            var fileName = "existing.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            var destFile = Path.Combine(_testDestinationPath, fileName);
            var fileContent = CreateLargeFileContent(101);
            
            File.WriteAllBytes(sourceFile, fileContent);
            File.WriteAllBytes(destFile, fileContent); // File already exists in destination

            // Act
            var filesToCheck = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");

            // Assert
            Assert.AreEqual(0, filesToCheck.Count(), "File should be skipped because it already exists");
            Assert.IsTrue(File.Exists(sourceFile), "Source file should remain");
            Assert.IsTrue(File.Exists(destFile), "Destination file should remain");
        }

        [TestMethod]
        public void EndToEnd_LockedFile_ReturnsAsLocked()
        {
            // Arrange
            var fileName = "locked.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            var fileContent = CreateLargeFileContent(101);
            File.WriteAllBytes(sourceFile, fileContent);

            // Act & Assert
            using (var fileStream = File.Open(sourceFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var filesToCheck = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");
                Assert.AreEqual(0, filesToCheck.Count(), "Locked file should not be selected for processing");
            }

            // After releasing the lock, the file should be available
            var filesToCheckAfter = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");
            Assert.AreEqual(1, filesToCheckAfter.Count(), "File should be available after lock is released");
        }

        [TestMethod]
        public void EndToEnd_CopyMode_LeavesSourceFile()
        {
            // Arrange
            var fileName = "copy_test.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            var destFile = Path.Combine(_testDestinationPath, fileName);
            var fileContent = CreateLargeFileContent(101);
            
            File.WriteAllBytes(sourceFile, fileContent);

            // Act
            var filesToCheck = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");
            var lockedFiles = FileCopyService.CopyFiles(_testDestinationPath, filesToCheck);

            // Assert
            Assert.AreEqual(1, filesToCheck.Count());
            Assert.AreEqual(0, lockedFiles.Count);
            Assert.IsTrue(File.Exists(sourceFile), "Source file should remain when copying");
            Assert.IsTrue(File.Exists(destFile), "Destination file should exist");
            
            var sourceContent = File.ReadAllBytes(sourceFile);
            var destContent = File.ReadAllBytes(destFile);
            Assert.AreEqual(sourceContent.Length, destContent.Length, "File content should match");
        }

        [TestMethod]
        public void EndToEnd_EmptySourceDirectory_ReturnsNoFiles()
        {
            // Act
            var filesToCheck = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");

            // Assert
            Assert.AreEqual(0, filesToCheck.Count(), "Empty directory should return no files");
        }

        [TestMethod]
        public void EndToEnd_AllExtensions_ProcessesAllValidFiles()
        {
            // Arrange
            var fileNames = new[] { "video.mkv", "audio.mp3", "document.txt", "image.jpg" };
            var fileContent = CreateLargeFileContent(101);

            foreach (var fileName in fileNames)
            {
                var sourceFile = Path.Combine(_testSourcePath, fileName);
                File.WriteAllBytes(sourceFile, fileContent);
            }

            // Act - Use empty extension to process all files
            var filesToCheck = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, "");
            var lockedFiles = FileCopyService.MoveFiles(_testDestinationPath, filesToCheck);

            // Assert
            Assert.AreEqual(4, filesToCheck.Count(), "All files should be selected when extension is empty");
            Assert.AreEqual(0, lockedFiles.Count);

            foreach (var fileName in fileNames)
            {
                var destFile = Path.Combine(_testDestinationPath, fileName);
                Assert.IsTrue(File.Exists(destFile), $"File {fileName} should exist in destination");
            }
        }
    }
}
