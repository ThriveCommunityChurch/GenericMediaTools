# Thrive FileCopy Service - Quick Start

## Installation (30 seconds)

### MSI Installer (Recommended)
1. Double-click on 'ThriveFileCopyInstaller.msi'
2. Click 'Yes' when Windows asks for administrator permission
3. Accept the MIT License agreement
4. Choose installation directory (default: C:\Program Files\Thrive Community Church\FileCopy Service)
5. Click Install - the service will be installed and ready to start

### Start the Service (Required)
After installation, you must manually start the service:
- **Services.msc**: Find "Thrive FileCopy Service" and click Start
- **PowerShell**: `Start-Service ThriveFileCopy`
- **Command Prompt**: `net start ThriveFileCopy`

The service will then start automatically on every system boot.

## Default Configuration
- **Source**: C:\temp\source-videos (system-accessible directory)
- **Destination**: C:\temp\recordings-backup
- **File Type**: .mkv files ≥100MB (configurable)
- **Check Interval**: Every 1 minute
- **Retry Delay**: 5 minutes for locked files
- **Service Account**: LocalSystem (for system-wide access)

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
