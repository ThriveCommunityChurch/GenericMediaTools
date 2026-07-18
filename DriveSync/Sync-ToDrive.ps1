<#
.SYNOPSIS
    Moves finished OBS recordings straight to Google Drive using rclone.

.DESCRIPTION
    Replacement for the manual robocopy-to-NAS + manual-upload-to-Drive workflow:

        Robocopy C:\Users\wyatt\Videos 'Z:\...\Recordings' *.mkv /r:3 /z /eta /min:100 /mov /mt:8 /timfix

    Each robocopy flag has an rclone equivalent applied below:
        *.mkv    -> --include (IncludePattern)
        /r:3     -> --retries (RetryCount)
        /z       -> chunked, resumable Drive uploads (--drive-chunk-size)
        /min:100 -> --min-size (MinFileSize)
        /mov     -> "rclone move" (source deleted only after the upload is verified)
        /mt:8    -> --transfers (Transfers; uploads are bandwidth-bound, so fewer is fine)
        /timfix  -> rclone preserves modification times on Drive by default

    Additions over robocopy:
        --min-age   skips a recording OBS is still writing
        --max-depth 1 matches robocopy's non-recursive default
        an optional NAS mirror leg (NasMirrorPath) runs BEFORE the Drive move,
        so the local file still exists when it is copied to the NAS

    All settings live in DriveSync.config.json next to this script; command-line
    parameters override the config file.

.NOTES
    Run interactively once before installing the scheduled task:
        powershell -NoProfile -ExecutionPolicy Bypass -File .\Sync-ToDrive.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [string]$Source,
    [string]$DriveRemote,
    [string]$NasMirrorPath,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Load config (script params win over config file values)
# ---------------------------------------------------------------------------
$configPath = Join-Path $PSScriptRoot 'DriveSync.config.json'
if (-not (Test-Path $configPath)) {
    throw "Config file not found: $configPath"
}
$config = Get-Content $configPath -Raw | ConvertFrom-Json

if (-not $Source)        { $Source        = $config.Source }
if (-not $DriveRemote)   { $DriveRemote   = $config.DriveRemote }
if (-not $PSBoundParameters.ContainsKey('NasMirrorPath')) { $NasMirrorPath = $config.NasMirrorPath }

$includePattern  = $config.IncludePattern
$minFileSize     = $config.MinFileSize
$minAgeMinutes   = [int]$config.MinAgeMinutes
$transfers       = [int]$config.Transfers
$retryCount      = [int]$config.RetryCount
$driveChunkSize  = "$([int]$config.DriveChunkSizeMB)M"
$logDir          = $config.LogDir

if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
$logFile = Join-Path $logDir ("drivesync_{0}.txt" -f (Get-Date -Format 'yyyyMMdd'))

function Write-SyncLog([string]$message) {
    $line = "[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $message
    Add-Content -Path $logFile -Value $line
    Write-Host $line
}

# ---------------------------------------------------------------------------
# Guard rails: rclone present, remote configured, single instance
# ---------------------------------------------------------------------------
$rclone = Get-Command rclone -ErrorAction SilentlyContinue
if (-not $rclone) {
    Write-SyncLog "ERROR: rclone not found on PATH. Install it first (see DriveSync README)."
    exit 1
}

$remoteName = ($DriveRemote -split ':')[0] + ':'
$configuredRemotes = & rclone listremotes 2>$null
if ($configuredRemotes -notcontains $remoteName) {
    Write-SyncLog "ERROR: rclone remote '$remoteName' is not configured. Run 'rclone config' (see DriveSync README)."
    exit 1
}

if (-not (Test-Path $Source)) {
    Write-SyncLog "ERROR: source folder '$Source' does not exist."
    exit 1
}

# A sermon recording can take longer to upload than the task interval, so make
# sure two runs never race each other over the same files.
$mutex = New-Object System.Threading.Mutex($false, 'Global\ThriveDriveSync')
if (-not $mutex.WaitOne(0)) {
    Write-SyncLog "Another DriveSync run is still in progress - skipping this pass."
    exit 0
}

try {
    $commonArgs = @(
        '--include', $includePattern
        '--min-size', $minFileSize
        '--min-age', "${minAgeMinutes}m"
        '--max-depth', '1'
        '--retries', "$retryCount"
        '--transfers', "$transfers"
        '--log-file', $logFile
        '--log-level', 'INFO'
        '--stats', '1m'
        '--stats-one-line'
    )
    if ($DryRun) { $commonArgs += '--dry-run' }

    # -----------------------------------------------------------------------
    # Leg 1 (optional): mirror to the NAS while the local file still exists
    # -----------------------------------------------------------------------
    if ($NasMirrorPath) {
        Write-SyncLog "Mirroring new recordings to NAS: $NasMirrorPath"
        & rclone copy $Source $NasMirrorPath @commonArgs
        if ($LASTEXITCODE -ne 0) {
            Write-SyncLog "WARNING: NAS mirror failed (exit $LASTEXITCODE). Skipping the Drive move so no file is deleted without a backup."
            exit $LASTEXITCODE
        }
    }

    # -----------------------------------------------------------------------
    # Leg 2: move to Google Drive (source file deleted only after rclone
    # verifies the upload - same end state as robocopy /mov)
    # -----------------------------------------------------------------------
    Write-SyncLog "Moving new recordings to Google Drive: $DriveRemote"
    & rclone move $Source $DriveRemote @commonArgs --drive-chunk-size $driveChunkSize
    if ($LASTEXITCODE -ne 0) {
        Write-SyncLog "WARNING: Drive move finished with exit code $LASTEXITCODE (files that failed remain in the source and retry next run)."
        exit $LASTEXITCODE
    }

    Write-SyncLog "DriveSync pass complete."
}
finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
