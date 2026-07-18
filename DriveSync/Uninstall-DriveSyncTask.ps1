<#
.SYNOPSIS
    Removes the "Thrive Media Sync" scheduled task (and the older
    "Thrive Drive Sync" task if present). Does not touch rclone, its config,
    or any files.
#>
$ErrorActionPreference = 'Stop'

$removed = $false
foreach ($taskName in @('Thrive Media Sync', 'Thrive Drive Sync')) {
    if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        Write-Host "Scheduled task '$taskName' removed."
        $removed = $true
    }
}
if (-not $removed) { Write-Host "No MediaSync scheduled task was installed." }
