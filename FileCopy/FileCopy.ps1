Param(
    [string]$SourceFolder,
    [string]$DestinationFolder
)

$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $SourceFolder
$watcher.Filter = "*.mkv"
$watcher.IncludeSubdirectories = $false
$watcher.EnableRaisingEvents = $true

$action = {
    $item = $eventArgs.FullPath
    # Run in retryable mode, with 3 retries, min file size of 100 bytes and fix the times when they get sent
    robocopy $item $DestinationFolder /r:3 /z /min:100 /mov /timfix
}

Register-ObjectEvent $watcher "Created" -Action $action

while ($true) {
    # The loop keeps the script running indefinitely
    Start-Sleep -Seconds 60
}
