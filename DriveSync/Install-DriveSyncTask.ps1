<#
.SYNOPSIS
    Registers the "Thrive Drive Sync" scheduled task that runs Sync-ToDrive.ps1
    every 10 minutes as the current user.

.DESCRIPTION
    The task runs as the logged-on user because rclone's Google Drive token is
    stored per-user (%APPDATA%\rclone\rclone.conf). Run this from a normal
    PowerShell prompt as the same user that ran 'rclone config':

        powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DriveSyncTask.ps1
#>
[CmdletBinding()]
param(
    [int]$IntervalMinutes = 10
)

$ErrorActionPreference = 'Stop'

$taskName   = 'Thrive Drive Sync'
$scriptPath = Join-Path $PSScriptRoot 'Sync-ToDrive.ps1'
if (-not (Test-Path $scriptPath)) { throw "Sync-ToDrive.ps1 not found next to this installer." }

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
    -Description 'Moves finished OBS recordings to Google Drive via rclone (GenericMediaTools/DriveSync).' `
    -Force | Out-Null

Write-Host "Scheduled task '$taskName' installed - runs every $IntervalMinutes minute(s) as $env:USERNAME."
Write-Host "Verify with: Get-ScheduledTask -TaskName '$taskName' | Get-ScheduledTaskInfo"
