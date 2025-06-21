using FileCopy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FileCopy.Tests
{
    [TestClass]
    public class WorkerTests
    {
        private Mock<ILogger> _mockLogger = null!;
        private string _testSourcePath = null!;
        private string _testDestinationPath = null!;
        private string _testConfigPath = null!;

        [TestInitialize]
        public void Setup()
        {
            // Create temporary directories for testing
            _testSourcePath = Path.Combine(Path.GetTempPath(), "WorkerTest_Source", Guid.NewGuid().ToString());
            _testDestinationPath = Path.Combine(Path.GetTempPath(), "WorkerTest_Dest", Guid.NewGuid().ToString());
            _testConfigPath = Path.Combine(Path.GetTempPath(), "WorkerTest_Config", Guid.NewGuid().ToString());
            
            Directory.CreateDirectory(_testSourcePath);
            Directory.CreateDirectory(_testDestinationPath);
            Directory.CreateDirectory(_testConfigPath);

            // Setup mock logger
            _mockLogger = new Mock<ILogger>();
            Log.Logger = _mockLogger.Object;

            // Create test configuration file
            CreateTestConfiguration();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test directories
            CleanupDirectory(_testSourcePath);
            CleanupDirectory(_testDestinationPath);
            CleanupDirectory(_testConfigPath);
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

        private void CreateTestConfiguration()
        {
            var configContent = $@"{{
  ""Logging"": {{
    ""LogLevel"": {{
      ""Default"": ""Information""
    }}
  }},
  ""SourcePath"": ""{_testSourcePath.Replace("\\", "\\\\")}"",
  ""DestinationPath"": ""{_testDestinationPath.Replace("\\", "\\\\")}"",
  ""DesiredFileExtension"": "".mkv"",
  ""DeleteOnCopy"": ""true"",
  ""Serilog"": {{
    ""Using"": [ ""Serilog.Sinks.Console"" ],
    ""MinimumLevel"": {{
      ""Default"": ""Information""
    }},
    ""WriteTo"": [
      {{
        ""Name"": ""Console""
      }}
    ]
  }}
}}";

            var configFile = Path.Combine(_testConfigPath, "appsettings.json");
            File.WriteAllText(configFile, configContent);
        }

        [TestMethod]
        public void Worker_Constructor_LoadsConfiguration()
        {
            // Arrange & Act
            var originalDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(_testConfigPath);
                var worker = new Worker();

                // Assert
                Assert.IsNotNull(worker);
                Assert.IsNotNull(worker.Configuration);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
            }
        }

        [TestMethod]
        public void Worker_Constructor_MissingSourcePath_ThrowsException()
        {
            // Arrange
            var tempConfigPath = Path.Combine(Path.GetTempPath(), "InvalidConfig1", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempConfigPath);

            var invalidConfigContent = @"{
  ""DestinationPath"": ""C:\\Temp\\Dest"",
  ""DesiredFileExtension"": "".mkv"",
  ""DeleteOnCopy"": ""true""
}";
            var invalidConfigFile = Path.Combine(tempConfigPath, "appsettings.json");
            File.WriteAllText(invalidConfigFile, invalidConfigContent);

            var originalDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempConfigPath);

                // Act & Assert
                Assert.ThrowsException<InvalidOperationException>(() => new Worker());
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
                CleanupDirectory(tempConfigPath);
            }
        }

        [TestMethod]
        public void Worker_Constructor_MissingDestinationPath_ThrowsException()
        {
            // Arrange
            var tempConfigPath = Path.Combine(Path.GetTempPath(), "InvalidConfig2", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempConfigPath);

            var invalidConfigContent = @"{
  ""SourcePath"": ""C:\\Temp\\Source"",
  ""DesiredFileExtension"": "".mkv"",
  ""DeleteOnCopy"": ""true""
}";
            var invalidConfigFile = Path.Combine(tempConfigPath, "appsettings.json");
            File.WriteAllText(invalidConfigFile, invalidConfigContent);

            var originalDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempConfigPath);

                // Act & Assert
                Assert.ThrowsException<InvalidOperationException>(() => new Worker());
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
                CleanupDirectory(tempConfigPath);
            }
        }

        [TestMethod]
        public async Task Worker_ExecuteAsync_CreatesSourceDirectoryIfMissing()
        {
            // Arrange
            var tempConfigPath = Path.Combine(Path.GetTempPath(), "WorkerTest_TempConfig", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempConfigPath);

            var nonExistentSource = Path.Combine(Path.GetTempPath(), "NonExistent", Guid.NewGuid().ToString());
            var configContent = $@"{{
  ""SourcePath"": ""{nonExistentSource.Replace("\\", "\\\\")}"",
  ""DestinationPath"": ""{_testDestinationPath.Replace("\\", "\\\\")}"",
  ""DesiredFileExtension"": "".mkv"",
  ""DeleteOnCopy"": ""true"",
  ""Serilog"": {{
    ""Using"": [ ""Serilog.Sinks.Console"" ],
    ""MinimumLevel"": {{ ""Default"": ""Information"" }},
    ""WriteTo"": [{{ ""Name"": ""Console"" }}]
  }}
}}";

            var configFile = Path.Combine(tempConfigPath, "appsettings.json");
            File.WriteAllText(configFile, configContent);

            var originalDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempConfigPath);
                var worker = new Worker();
                var cancellationTokenSource = new CancellationTokenSource();

                // Cancel after a short delay to prevent infinite loop
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2));

                // Act
                await worker.StartAsync(cancellationTokenSource.Token);

                // Give it a moment to create the directory
                await Task.Delay(100);

                await worker.StopAsync(cancellationTokenSource.Token);

                // Assert
                Assert.IsTrue(Directory.Exists(nonExistentSource));
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
                CleanupDirectory(nonExistentSource);
                CleanupDirectory(tempConfigPath);
            }
        }

        [TestMethod]
        public async Task Worker_ExecuteAsync_HandlesOperationCancelledException()
        {
            // Arrange
            var tempConfigPath = Path.Combine(Path.GetTempPath(), "WorkerTest_CancelConfig", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempConfigPath);

            var configContent = $@"{{
  ""SourcePath"": ""{_testSourcePath.Replace("\\", "\\\\")}"",
  ""DestinationPath"": ""{_testDestinationPath.Replace("\\", "\\\\")}"",
  ""DesiredFileExtension"": "".mkv"",
  ""DeleteOnCopy"": ""true"",
  ""Serilog"": {{
    ""Using"": [ ""Serilog.Sinks.Console"" ],
    ""MinimumLevel"": {{ ""Default"": ""Information"" }},
    ""WriteTo"": [{{ ""Name"": ""Console"" }}]
  }}
}}";

            var configFile = Path.Combine(tempConfigPath, "appsettings.json");
            File.WriteAllText(configFile, configContent);

            var originalDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempConfigPath);
                var worker = new Worker();
                var cancellationTokenSource = new CancellationTokenSource();

                // Act
                var task = worker.StartAsync(cancellationTokenSource.Token);
                cancellationTokenSource.Cancel(); // Cancel immediately

                // Assert - Should not throw exception
                await worker.StopAsync(CancellationToken.None);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
                CleanupDirectory(tempConfigPath);
            }
        }

        [TestMethod]
        public void Worker_Configuration_ParsesDeleteOnCopyCorrectly()
        {
            // Arrange
            var configWithFalse = $@"{{
  ""SourcePath"": ""{_testSourcePath.Replace("\\", "\\\\")}"",
  ""DestinationPath"": ""{_testDestinationPath.Replace("\\", "\\\\")}"",
  ""DesiredFileExtension"": "".mkv"",
  ""DeleteOnCopy"": ""false"",
  ""Serilog"": {{
    ""Using"": [ ""Serilog.Sinks.Console"" ],
    ""MinimumLevel"": {{ ""Default"": ""Information"" }},
    ""WriteTo"": [{{ ""Name"": ""Console"" }}]
  }}
}}";

            var configFile = Path.Combine(_testConfigPath, "appsettings.json");
            File.WriteAllText(configFile, configWithFalse);

            var originalDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(_testConfigPath);
                
                // Act
                var worker = new Worker();

                // Assert
                Assert.IsNotNull(worker.Configuration);
                Assert.AreEqual("false", worker.Configuration["DeleteOnCopy"]);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
            }
        }
    }
}
