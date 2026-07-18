<#
.SYNOPSIS
    Registers the "Thrive Media Sync" scheduled task that runs MediaSync.ps1
    every 10 minutes.

.DESCRIPTION
    Because Drive auth uses a service-account key file (not a per-user browser
    token), the task just needs to run as a user that can read the key file
    and reach the NAS UNC path. Run from a normal PowerShell prompt:

        powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DriveSyncTask.ps1

    Re-running the installer updates the existing task in place.
#>
[CmdletBinding()]
param(
    [int]$IntervalMinutes = 10
)

$ErrorActionPreference = 'Stop'

$taskName   = 'Thrive Media Sync'
$scriptPath = Join-Path $PSScriptRoot 'MediaSync.ps1'
if (-not (Test-Path $scriptPath)) { throw "MediaSync.ps1 not found next to this installer." }

# clean up the task from the pre-service-account version of this tool
if (Get-ScheduledTask -TaskName 'Thrive Drive Sync' -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName 'Thrive Drive Sync' -Confirm:$false
    Write-Host "Removed old 'Thrive Drive Sync' task."
}

$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$scriptPath`""

# Repeat every N minutes, indefinitely, starting one minute from install time
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) `
    -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes)

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Hours 12)

Register-ScheduledTask -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Description 'Archives OBS recordings to the NAS, uploads MKV archive + final MP4s to Google Drive, enforces MKV retention (GenericMediaTools/DriveSync).' `
    -Force | Out-Null

Write-Host "Scheduled task '$taskName' installed - runs every $IntervalMinutes minute(s) as $env:USERNAME."
Write-Host "Verify with: Get-ScheduledTask -TaskName '$taskName' | Get-ScheduledTaskInfo"
