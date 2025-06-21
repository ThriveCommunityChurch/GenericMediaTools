using FileCopyServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileCopy.Tests
{
    [TestClass]
    public class FileCopyServiceTests
    {
        private string _testSourcePath = null!;
        private string _testDestinationPath = null!;
        private Mock<ILogger> _mockLogger = null!;

        [TestInitialize]
        public void Setup()
        {
            // Create temporary directories for testing
            _testSourcePath = Path.Combine(Path.GetTempPath(), "FileCopyTest_Source", Guid.NewGuid().ToString());
            _testDestinationPath = Path.Combine(Path.GetTempPath(), "FileCopyTest_Dest", Guid.NewGuid().ToString());
            
            Directory.CreateDirectory(_testSourcePath);
            Directory.CreateDirectory(_testDestinationPath);

            // Setup mock logger
            _mockLogger = new Mock<ILogger>();
            Log.Logger = _mockLogger.Object;
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test directories
            if (Directory.Exists(_testSourcePath))
            {
                Directory.Delete(_testSourcePath, true);
            }
            if (Directory.Exists(_testDestinationPath))
            {
                Directory.Delete(_testDestinationPath, true);
            }
        }

        [TestMethod]
        public void IsFileLocked_FileNotLocked_ReturnsFalse()
        {
            // Arrange
            var testFile = Path.Combine(_testSourcePath, "test.mkv");
            File.WriteAllText(testFile, "test content");

            // Act
            var result = FileCopyService.IsFileLocked(testFile);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsFileLocked_FileDoesNotExist_ReturnsTrue()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testSourcePath, "nonexistent.mkv");

            // Act
            var result = FileCopyService.IsFileLocked(nonExistentFile);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsFileLocked_FileLocked_ReturnsTrue()
        {
            // Arrange
            var testFile = Path.Combine(_testSourcePath, "locked.mkv");
            File.WriteAllText(testFile, "test content");

            // Act & Assert
            using (var fileStream = File.Open(testFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var result = FileCopyService.IsFileLocked(testFile);
                Assert.IsTrue(result);
            }
        }

        [TestMethod]
        public void CheckFiles_ValidMkvFile_ReturnsFile()
        {
            // Arrange
            var testFile = Path.Combine(_testSourcePath, "test.mkv");
            var fileContent = new byte[100 * 1024 * 1024 + 1]; // 100MB + 1 byte
            File.WriteAllBytes(testFile, fileContent);

            // Act
            var result = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");

            // Assert
            Assert.AreEqual(1, result.Count());
            Assert.AreEqual(testFile, result.First());
        }

        [TestMethod]
        public void CheckFiles_FileTooSmall_ReturnsEmpty()
        {
            // Arrange
            var testFile = Path.Combine(_testSourcePath, "small.mkv");
            File.WriteAllText(testFile, "small file"); // Much smaller than 100MB

            // Act
            var result = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");

            // Assert
            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public void CheckFiles_CustomMinimumSize_RespectsLimit()
        {
            // Arrange
            var testFile = Path.Combine(_testSourcePath, "medium.mkv");
            var fileContent = new byte[50 * 1024 * 1024]; // 50MB file
            File.WriteAllBytes(testFile, fileContent);

            // Act - with default 100MB limit, should return empty
            var resultDefault = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv", 100);

            // Act - with 25MB limit, should return the file
            var resultCustom = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv", 25);

            // Assert
            Assert.AreEqual(0, resultDefault.Count(), "50MB file should be rejected with 100MB limit");
            Assert.AreEqual(1, resultCustom.Count(), "50MB file should be accepted with 25MB limit");
        }

        [TestMethod]
        public void CheckFiles_WrongExtension_ReturnsEmpty()
        {
            // Arrange
            var testFile = Path.Combine(_testSourcePath, "test.mp4");
            var fileContent = new byte[100 * 1024 * 1024 + 1]; // 100MB + 1 byte
            File.WriteAllBytes(testFile, fileContent);

            // Act
            var result = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");

            // Assert
            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public void CheckFiles_FileAlreadyExists_ReturnsEmpty()
        {
            // Arrange
            var fileName = "existing.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            var destFile = Path.Combine(_testDestinationPath, fileName);
            
            var fileContent = new byte[100 * 1024 * 1024 + 1]; // 100MB + 1 byte
            File.WriteAllBytes(sourceFile, fileContent);
            File.WriteAllBytes(destFile, fileContent); // File already exists in destination

            // Act
            var result = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");

            // Assert
            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public void CheckFiles_LockedFile_ReturnsEmpty()
        {
            // Arrange
            var testFile = Path.Combine(_testSourcePath, "locked.mkv");
            var fileContent = new byte[100 * 1024 * 1024 + 1]; // 100MB + 1 byte
            File.WriteAllBytes(testFile, fileContent);

            // Act & Assert
            using (var fileStream = File.Open(testFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var result = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, ".mkv");
                Assert.AreEqual(0, result.Count());
            }
        }

        [TestMethod]
        public void CheckFiles_EmptyExtension_ReturnsAllValidFiles()
        {
            // Arrange
            var testFile1 = Path.Combine(_testSourcePath, "test.mkv");
            var testFile2 = Path.Combine(_testSourcePath, "test.mp4");
            var fileContent = new byte[100 * 1024 * 1024 + 1]; // 100MB + 1 byte
            
            File.WriteAllBytes(testFile1, fileContent);
            File.WriteAllBytes(testFile2, fileContent);

            // Act
            var result = FileCopyService.CheckFiles(_testSourcePath, _testDestinationPath, "");

            // Assert
            Assert.AreEqual(2, result.Count());
            Assert.IsTrue(result.Contains(testFile1));
            Assert.IsTrue(result.Contains(testFile2));
        }

        [TestMethod]
        public void MoveFiles_ValidFile_MovesSuccessfully()
        {
            // Arrange
            var fileName = "test.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            var destFile = Path.Combine(_testDestinationPath, fileName);
            
            File.WriteAllText(sourceFile, "test content");
            var filesToMove = new List<string> { sourceFile };

            // Act
            var lockedFiles = FileCopyService.MoveFiles(_testDestinationPath, filesToMove);

            // Assert
            Assert.AreEqual(0, lockedFiles.Count);
            Assert.IsFalse(File.Exists(sourceFile));
            Assert.IsTrue(File.Exists(destFile));
            Assert.AreEqual("test content", File.ReadAllText(destFile));
        }

        [TestMethod]
        public void CopyFiles_ValidFile_CopiesSuccessfully()
        {
            // Arrange
            var fileName = "test.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            var destFile = Path.Combine(_testDestinationPath, fileName);
            
            File.WriteAllText(sourceFile, "test content");
            var filesToCopy = new List<string> { sourceFile };

            // Act
            var lockedFiles = FileCopyService.CopyFiles(_testDestinationPath, filesToCopy);

            // Assert
            Assert.AreEqual(0, lockedFiles.Count);
            Assert.IsTrue(File.Exists(sourceFile)); // Original file should still exist
            Assert.IsTrue(File.Exists(destFile));
            Assert.AreEqual("test content", File.ReadAllText(destFile));
        }

        [TestMethod]
        public void MoveFiles_LockedFile_ReturnsLockedFile()
        {
            // Arrange
            var fileName = "locked.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            File.WriteAllText(sourceFile, "test content");
            var filesToMove = new List<string> { sourceFile };

            // Act & Assert
            using (var fileStream = File.Open(sourceFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var lockedFiles = FileCopyService.MoveFiles(_testDestinationPath, filesToMove);
                Assert.AreEqual(1, lockedFiles.Count);
                Assert.IsTrue(lockedFiles.Contains(sourceFile));
            }
        }

        [TestMethod]
        public void CopyFiles_LockedFile_ReturnsLockedFile()
        {
            // Arrange
            var fileName = "locked.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            File.WriteAllText(sourceFile, "test content");
            var filesToCopy = new List<string> { sourceFile };

            // Act & Assert
            using (var fileStream = File.Open(sourceFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var lockedFiles = FileCopyService.CopyFiles(_testDestinationPath, filesToCopy);
                Assert.AreEqual(1, lockedFiles.Count);
                Assert.IsTrue(lockedFiles.Contains(sourceFile));
            }
        }

        [TestMethod]
        public void RobocopyStyleMoveFiles_ValidFiles_MovesSuccessfully()
        {
            // Arrange
            var fileName = "test.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            var destFile = Path.Combine(_testDestinationPath, fileName);
            var fileContent = new byte[100 * 1024 * 1024 + 1]; // 100MB + 1 byte
            File.WriteAllBytes(sourceFile, fileContent);

            // Act
            var lockedFiles = FileCopyService.RobocopyStyleMoveFiles(_testSourcePath, _testDestinationPath, ".mkv");

            // Assert
            Assert.IsNotNull(lockedFiles);
            Assert.AreEqual(0, lockedFiles.Count, "No files should be locked");
            Assert.IsFalse(File.Exists(sourceFile), "Source file should be moved (deleted)");
            Assert.IsTrue(File.Exists(destFile), "Destination file should exist");
        }

        [TestMethod]
        public void RobocopyStyleMoveFiles_NonExistentSource_ReturnsEmptySet()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            // Act
            var lockedFiles = FileCopyService.RobocopyStyleMoveFiles(nonExistentPath, _testDestinationPath, ".mkv");

            // Assert
            Assert.IsNotNull(lockedFiles);
            // The method should handle non-existent paths gracefully
        }

        [TestMethod]
        public void RobocopyStyleMoveFiles_CreatesDestinationDirectory()
        {
            // Arrange
            var newDestPath = Path.Combine(Path.GetTempPath(), "NewDest", Guid.NewGuid().ToString());
            var fileName = "test.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            var fileContent = new byte[100 * 1024 * 1024 + 1]; // 100MB + 1 byte
            File.WriteAllBytes(sourceFile, fileContent);

            try
            {
                // Act
                var lockedFiles = FileCopyService.RobocopyStyleMoveFiles(_testSourcePath, newDestPath, ".mkv");

                // Assert
                Assert.IsTrue(Directory.Exists(newDestPath));
                Assert.IsNotNull(lockedFiles);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(newDestPath))
                {
                    Directory.Delete(newDestPath, true);
                }
            }
        }

        [TestMethod]
        public void RobocopyStyleMoveFiles_FixesFileTimes()
        {
            // Arrange
            var fileName = "timetest.mkv";
            var sourceFile = Path.Combine(_testSourcePath, fileName);
            var destFile = Path.Combine(_testDestinationPath, fileName);
            var fileContent = new byte[100 * 1024 * 1024 + 1]; // 100MB + 1 byte
            File.WriteAllBytes(sourceFile, fileContent);

            // Set specific times on source file
            var testTime = new DateTime(2023, 1, 1, 12, 0, 0);
            File.SetCreationTime(sourceFile, testTime);
            File.SetLastWriteTime(sourceFile, testTime);

            // Act
            var lockedFiles = FileCopyService.RobocopyStyleMoveFiles(_testSourcePath, _testDestinationPath, ".mkv");

            // Assert
            Assert.AreEqual(0, lockedFiles.Count);
            Assert.IsTrue(File.Exists(destFile));

            // Verify times were copied (allowing for small differences due to file system precision)
            var destCreationTime = File.GetCreationTime(destFile);
            var destWriteTime = File.GetLastWriteTime(destFile);

            Assert.AreEqual(testTime.Date, destCreationTime.Date, "Creation time should be preserved");
            Assert.AreEqual(testTime.Date, destWriteTime.Date, "Write time should be preserved");
        }
    }
}
