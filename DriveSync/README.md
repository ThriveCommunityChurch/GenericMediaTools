# Drive Sync

Automates the last manual step of the recording workflow: getting finished OBS
recordings into **Google Drive** without robocopy, PowerShell-by-hand, or a
browser upload.

It replaces this command:

```
Robocopy C:\Users\wyatt\Videos 'Z:\Wyatt\ThriveLiveBackups\Recordings' *.mkv /r:3 /z /eta /min:100 /mov /mt:8 /timfix
```

with a scheduled task that runs [rclone](https://rclone.org) every 10 minutes
and **moves** new `.mkv` recordings straight to Google Drive. Files are deleted
locally only after rclone has verified the upload (checksum match), so the end
state is the same as robocopy's `/mov` — just with Drive as the destination.

## How each robocopy flag is covered

| Robocopy | DriveSync equivalent |
|----------|----------------------|
| `*.mkv` | `IncludePattern` config → `--include "*.mkv"` |
| `/r:3` | `RetryCount` config → `--retries 3` |
| `/z` (restartable) | Drive uploads are chunked + resumable (`--drive-chunk-size`) |
| `/min:100` | `MinFileSize` config → `--min-size 100b` |
| `/mov` | `rclone move` — delete-after-verified-copy |
| `/mt:8` | `Transfers` config (default 4 — uploads are bandwidth-bound, more threads don't help) |
| `/timfix` | rclone preserves modification times on Drive by default |
| _(new)_ | `MinAgeMinutes` (default 10) skips a file OBS is still recording |
| _(new)_ | optional `NasMirrorPath` keeps a NAS backup copy before the Drive move |

## One-time setup (on the recording PC)

### 1. Install rclone

```powershell
winget install Rclone.Rclone
```

(or download the zip from https://rclone.org/downloads/ and put `rclone.exe`
somewhere on the PATH). Open a **new** PowerShell window afterward and confirm
`rclone version` works.

### 2. Connect rclone to Google Drive

```powershell
rclone config
```

- `n` (new remote), name it **`gdrive`** (the config file assumes this name)
- Storage type: `drive` (Google Drive)
- Leave `client_id` / `client_secret` blank (fine for this volume of uploads)
- Scope: `1` (full Drive access) — needed if the destination folder already
  exists / was created by hand
- Leave everything else at defaults; say **Yes** to auto-config so it opens a
  browser to sign in to the church Google account
- If the uploads should land in a **Shared Drive**, answer Yes to
  "Configure this as a Shared Drive (Team Drive)?" and pick it

Sanity check — this should list the Drive's top-level folders:

```powershell
rclone lsd gdrive:
```

### 3. Review the config

Edit `DriveSync.config.json`:

- `Source` — where OBS writes recordings (default `C:\Users\wyatt\Videos`)
- `DriveRemote` — destination folder on Drive (default
  `gdrive:ThriveLiveBackups/Recordings`; rclone creates it if missing)
- `NasMirrorPath` — leave `""` for Drive-only, or set to
  `Z:\\Wyatt\\ThriveLiveBackups\\Recordings` to also keep the NAS copy (the NAS
  copy happens first; if it fails, nothing is deleted locally)
- `LogDir` — defaults to `C:\logs\Thrive`, alongside the FileCopy tool's logs

### 4. Test it

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Sync-ToDrive.ps1 -DryRun
```

`-DryRun` shows what would be uploaded/deleted without doing it. Run again
without `-DryRun` to do a real pass.

### 5. Install the scheduled task

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DriveSyncTask.ps1
```

Runs every 10 minutes as the current user (rclone's Drive token is per-user),
hidden, skipping a pass if the previous one is still uploading. Change the
cadence with `-IntervalMinutes N`. Remove it with `Uninstall-DriveSyncTask.ps1`.

## Day-to-day behavior

1. OBS finishes writing `Recording.mkv` into the source folder.
2. Within ~10–20 minutes (interval + `MinAgeMinutes` in-use guard) the task
   picks it up.
3. Optional: file is copied to the NAS mirror path.
4. File is uploaded to Drive in resumable chunks, checksum-verified, then the
   local copy is deleted.
5. Everything is logged to `C:\logs\Thrive\drivesync_YYYYMMDD.txt` (one file
   per day). A failed upload leaves the file in place and it retries on the
   next pass.

## Notes / gotchas

- **Google's upload quota** is 750 GB/day per account — far beyond weekly
  sermon volume, but worth knowing.
- If the Google account password changes or access is revoked, the rclone
  token stops working; re-run `rclone config reconnect gdrive:`. The sync
  script logs a clear error and leaves files untouched when the remote is
  unreachable.
- The task must run as the user whose profile holds the rclone config. If the
  recording PC auto-logs-in as a different account, run `rclone config` as
  that account and re-run the installer from that account.
