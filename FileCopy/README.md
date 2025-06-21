# Thrive FileCopy Service

A lightweight Windows service that automatically moves OBS recording files (or really any file) from your local drive to network storage. This tool eliminates the need for manual file management and PowerShell scripts by running as a background service.

## 🚀 Quick Start

**Ready-to-use installer package:** `ThriveFileCopyInstaller-v1.0.0-SingleFile.zip`

1. Extract the ZIP file
2. Right-click `Install.bat` and select "Run as administrator"
3. Done! The service starts automatically

## ✨ Features

- **Single File Deployment**: Just one 14MB executable - no DLL files!
- **Auto-Configuration Detection**: Service automatically detects config changes
- **File Lock Detection**: Won't interfere with OBS recording
- **Smart Retry Logic**: Retries locked files after 5 minutes
- **Size Filtering**: Only processes files ≥100MB (configurable)
- **Comprehensive Logging**: Daily rotation with detailed troubleshooting
- **Easy Configuration**: Interactive tools and command-line options
- **Network Storage Support**: Works with mapped drives and UNC paths

## Package Contents

- **FileCopy.exe** - Single self-contained executable
- **Install.bat** - One-click installer
- **Configure.bat** - Easy configuration tool
- **Comprehensive Documentation** - Step-by-step guides

## 🔧 Configuration

**Super Easy:** Right-click `Configure.bat` → "Run as administrator"

**Configuration File:** `C:\Program Files\Thrive Community Church\FileCopy Service\appsettings.json`

**Auto-Detection:** Service automatically detects config changes within 60 seconds

## Monitoring

- **Service Status**: `Get-Service ThriveFileCopy`
- **Logs**: `C:\logs\Thrive\filecopy_log.txt`
- **Live Monitoring**: `Get-Content "C:\logs\Thrive\filecopy_log.txt" -Wait -Tail 10`

## Development

The service is built with .NET 8.0 and includes:
- **FileCopy** - Main service application
- **FileCopyServices** - Core file processing logic
- **FileCopy.Tests** - Unit tests
- **FileCopy.Installer** - MSI installer project

## 📋 Requirements Met

- ✅ Runs silently in background (Windows service)
- ✅ Deletes files after successful copy (configurable)
- ✅ Comprehensive logging with rotation
- ✅ Handles locked files (OBS recording detection)
- ✅ Configurable check interval (default: 60 seconds)
- ✅ Native C# implementation (no external robocopy dependency)
