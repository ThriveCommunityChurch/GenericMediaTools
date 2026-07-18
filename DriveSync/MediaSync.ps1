<#
.SYNOPSIS
    Config-driven media sync: archives OBS recordings to the NAS, uploads the
    MKV archive + final MP4s to Google Drive (service account), and enforces
    the 1-year MKV retention policy.

.DESCRIPTION
    Runs everything defined in DriveSync.config.json, in two passes:

      1. Retention  - deletes files older than MaxAgeDays from each configured
                      path (NAS and/or Drive). Runs FIRST so an expiring MKV is
                      removed from the NAS and Drive in the same pass and can't
                      be re-uploaded by a later sync job.
      2. Jobs       - each job is an rclone "move" (archive: source deleted
                      after the copy is checksum-verified) or "copy" (source
                      kept) between any two of: local folder, UNC/NAS path, or
                      an rclone remote (gdrive:...).

    Deploy this same folder to every PC that produces recordings; only the
    config differs per machine (see config.samples\).

    Safety rails on every job:
      --min-age     never touches a file OBS is still writing
      --max-depth 1 top-level files only (matches the old robocopy behavior)
      global mutex  a long upload can't race the next scheduled pass
      move jobs delete the source only after rclone verifies the transfer

.NOTES
    Test any config change first:
        powershell -NoProfile -ExecutionPolicy Bypass -File .\MediaSync.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'DriveSync.config.json'),
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ConfigPath)) { throw "Config file not found: $ConfigPath" }
$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json

$defaults = $config.Defaults
$logDir   = $config.LogDir
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
$logFile = Join-Path $logDir ("mediasync_{0}.txt" -f (Get-Date -Format 'yyyyMMdd'))

function Write-SyncLog([string]$message) {
    $line = "[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $message
    Add-Content -Path $logFile -Value $line
    Write-Host $line
}

function Test-IsRemotePath([string]$path) {
    # "gdrive:Archive" is a remote; "C:\Videos" and "\\nas\share" are not
    return ($path -match '^[^\\/]{2,}:')
}

# ---------------------------------------------------------------------------
# Guard rails
# ---------------------------------------------------------------------------
if (-not (Get-Command rclone -ErrorAction SilentlyContinue)) {
    Write-SyncLog "ERROR: rclone not found on PATH. Install it first (see README)."
    exit 1
}

$configuredRemotes = & rclone listremotes 2>$null
$allPaths = @($config.Jobs | ForEach-Object { $_.Source; $_.Destination }) + @($config.Retention | ForEach-Object { $_.Path })
foreach ($p in ($allPaths | Where-Object { $_ -and (Test-IsRemotePath $_) })) {
    $remoteName = ($p -split ':')[0] + ':'
    if ($configuredRemotes -notcontains $remoteName) {
        Write-SyncLog "ERROR: rclone remote '$remoteName' is not configured. See the service-account setup in the README."
        exit 1
    }
}

$mutex = New-Object System.Threading.Mutex($false, 'Global\ThriveMediaSync')
if (-not $mutex.WaitOne(0)) {
    Write-SyncLog "Another MediaSync run is still in progress - skipping this pass."
    exit 0
}

$overallExit = 0

try {
    if ($DryRun) { Write-SyncLog "=== DRY RUN - nothing will be copied, moved, or deleted ===" }

    $baseArgs = @(
        '--max-depth', '1'
        '--retries', "$([int]$defaults.Retries)"
        '--log-file', $logFile
        '--log-level', 'INFO'
        '--stats', '1m'
        '--stats-one-line'
    )
    if ($DryRun) { $baseArgs += '--dry-run' }

    # -----------------------------------------------------------------------
    # Pass 1: retention (runs first - see .DESCRIPTION)
    # -----------------------------------------------------------------------
    foreach ($rule in @($config.Retention)) {
        if (-not $rule) { continue }
        if (-not (Test-IsRemotePath $rule.Path) -and -not (Test-Path $rule.Path)) {
            Write-SyncLog "Retention '$($rule.Name)': path '$($rule.Path)' not reachable - skipping."
            continue
        }

        Write-SyncLog "Retention '$($rule.Name)': deleting '$($rule.Include)' older than $($rule.MaxAgeDays) days from '$($rule.Path)'"
        & rclone delete $rule.Path --include $rule.Include --min-age "$([int]$rule.MaxAgeDays)d" @baseArgs
        if ($LASTEXITCODE -ne 0) {
            Write-SyncLog "WARNING: retention '$($rule.Name)' finished with exit code $LASTEXITCODE."
            $overallExit = $LASTEXITCODE
        }
    }

    # -----------------------------------------------------------------------
    # Pass 2: sync jobs, in config order (NAS archive before Drive upload)
    # -----------------------------------------------------------------------
    foreach ($job in @($config.Jobs)) {
        if (-not $job) { continue }
        if (-not (Test-IsRemotePath $job.Source) -and -not (Test-Path $job.Source)) {
            Write-SyncLog "Job '$($job.Name)': source '$($job.Source)' not reachable - skipping."
            continue
        }

        $verb = if ($job.Mode -eq 'move') { 'move' } else { 'copy' }
        $jobArgs = $baseArgs + @(
            '--include', $job.Include
            '--min-size', $job.MinFileSize
            '--min-age', "$([int]$job.MinAgeMinutes)m"
            '--transfers', "$([int]$defaults.Transfers)"
        )
        # keep a sync job from re-uploading something retention just expired
        if ($job.MaxAgeDays) { $jobArgs += @('--max-age', "$([int]$job.MaxAgeDays)d") }
        if (Test-IsRemotePath $job.Destination) {
            $jobArgs += @('--drive-chunk-size', "$([int]$defaults.DriveChunkSizeMB)M")
        }

        Write-SyncLog "Job '$($job.Name)': rclone $verb '$($job.Source)' -> '$($job.Destination)'"
        & rclone $verb $job.Source $job.Destination @jobArgs
        if ($LASTEXITCODE -ne 0) {
            Write-SyncLog "WARNING: job '$($job.Name)' finished with exit code $LASTEXITCODE (failed files stay put and retry next pass)."
            $overallExit = $LASTEXITCODE
        }
    }

    Write-SyncLog "MediaSync pass complete (exit $overallExit)."
    exit $overallExit
}
finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
