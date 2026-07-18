<#
.SYNOPSIS
    Removes the "Thrive Drive Sync" scheduled task. Does not touch rclone,
    its config, or any files.
#>
$ErrorActionPreference = 'Stop'

$taskName = 'Thrive Drive Sync'
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    Write-Host "Scheduled task '$taskName' removed."
}
else {
    Write-Host "Scheduled task '$taskName' was not installed."
}
