# Thrive FileCopy Service - Quick Start (Single File Edition)

## Installation (30 seconds)

### Option 1: MSI Installer (Recommended)
1. Double-click on 'ThriveFileCopyInstaller.msi' (will automatically request admin privileges)
2. Click 'Yes' when Windows asks for administrator permission
3. Follow the installation wizard
4. The service will start automatically after installation

### Option 2: Single File Installer
1. Right-click on 'Install.bat' and select 'Run as administrator'
2. Follow the prompts
3. Done! Just one FileCopy.exe file (14MB) - no DLLs!

## Default Configuration
- **Source**: %USERPROFILE%\Videos (current user's Videos folder)
- **Destination**: Z:\Backups\Recordings
- **File Type**: .mkv files ≥100MB (configurable)
- **Check Interval**: Every 1 minute
- **Retry Delay**: 5 minutes for locked files

## Monitoring
- **Service Name**: ThriveFileCopy
- **Logs**: C:\logs\Thrive\filecopy_log.txt
- **Status Check**: Services.msc or `Get-Service ThriveFileCopy`

## Configuration
After installation, you can modify settings by editing:
`C:\Program Files\Thrive Community Church\FileCopy Service\appsettings.json`

Then restart the service:
`Restart-Service ThriveFileCopy`

## Features
- ✅ Automatic file monitoring
- ✅ File lock detection (won't interfere with OBS recording)
- ✅ Retry logic for locked files  
- ✅ Robocopy-style functionality (native C# implementation)
- ✅ Comprehensive logging
- ✅ Windows service (runs in background)

## Support
For issues, check the log file or visit:
https://github.com/ThriveCommunityChurch/GenericMediaTools
