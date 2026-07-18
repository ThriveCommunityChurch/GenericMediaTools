# Media Sync (DriveSync)

Fully automates the recording pipeline so nothing is ever copied or uploaded
by hand again:

1. **Raw MKV archive → NAS** — every PC that records (streaming PC, recording
   PC) automatically moves finished OBS `.mkv` files to the NAS archive.
   Replaces the manual robocopy step.
2. **Drive uploads via service account** — the NAS MKV archive and the final
   `.mp4` recordings are uploaded to a shared Google Drive location using a
   **service account**: no browser sign-in, no user session, fully headless.
3. **MKV retention: 1 year** — `.mkv` files older than 365 days are
   automatically deleted from the NAS archive and the Drive archive. Final
   `.mp4`s are kept forever.

Everything runs from one script (`MediaSync.ps1`) driven by one config file
(`DriveSync.config.json`), fired every 10 minutes by a hidden scheduled task.
Deploy the same folder to each PC — only the config differs (see
`config.samples\`).

## How it works

Each scheduled pass runs, via [rclone](https://rclone.org):

1. **Retention first** — `rclone delete --min-age 365d` on each configured
   path. Running retention before the sync jobs (plus a matching `MaxAgeDays`
   cap on the archive-upload job) means an expired MKV can't sneak back onto
   Drive from the NAS. On Drive, deletes go to the trash (auto-purged by
   Google after 30 days), so there's a grace window.
2. **Sync jobs in config order**:
   - `move` jobs (MKV → NAS) delete the source **only after** rclone
     checksum-verifies the transfer — same end state as robocopy `/mov`.
   - `copy` jobs (NAS → Drive, MP4s → Drive) leave the source in place;
     already-uploaded files are skipped by size + modtime.

Safety rails on every job: `--min-age 10m` so a file OBS is still writing is
never touched, top-level files only (matches the old robocopy behavior), a
global mutex so a long upload can't collide with the next pass, and failed
files simply stay put and retry on the next pass.

Logs: `C:\logs\Thrive\mediasync_YYYYMMDD.txt` (one file per day, includes
every transfer and every retention delete).

## One-time setup

### 1. Google Cloud service account (once, from any machine)

1. Go to https://console.cloud.google.com → create (or pick) a project, e.g.
   `thrive-media-sync`.
2. **APIs & Services → Library** → enable **Google Drive API**.
3. **IAM & Admin → Service Accounts** → Create service account (e.g.
   `media-sync`). No project roles needed.
4. Open the service account → **Keys → Add key → JSON** → download the key
   file. On each PC, place it at `C:\ProgramData\Thrive\gdrive-sa.json` and
   keep it out of any repo — it's a credential.
5. Copy the service account's email (`media-sync@...iam.gserviceaccount.com`).

### 2. Share the Drive destination with the service account

**Strongly recommended: a Shared Drive (Team Drive).** In Google Drive, create
a Shared Drive (e.g. **Thrive Media**), then add the service account's email
as a member with **Content manager** access. Files in a Shared Drive belong to
the drive, so uploads count against the church Workspace storage.

> ⚠️ A plain "shared folder" in someone's My Drive also works (share the
> folder with the service account's email), **but** files a service account
> uploads there are *owned by the service account*, which only has ~15 GB of
> its own quota — multi-GB sermon videos will hit that wall fast. If you
> don't have Workspace/Shared Drives, tell me and we'll fall back to the
> OAuth (browser sign-in) flow instead.

### 3. rclone (on each PC)

```powershell
winget install Rclone.Rclone
```

Then create the remote — no browser involved. For a **Shared Drive**, grab
its ID from the URL (`https://drive.google.com/drive/folders/<ID>` when
viewing the Shared Drive root) and run:

```powershell
rclone config create gdrive drive scope drive service_account_file C:\ProgramData\Thrive\gdrive-sa.json team_drive <SHARED_DRIVE_ID>
```

(For a plain shared folder instead: replace `team_drive <ID>` with
`root_folder_id <FOLDER_ID>`.)

Sanity check — should list the Shared Drive's folders without opening a
browser:

```powershell
rclone lsd gdrive:
```

### 4. Config (per PC)

Copy the right sample over `DriveSync.config.json`:

- **Streaming PC** — `config.samples\streaming-pc.config.json`: MKV → NAS,
  NAS archive → Drive, final MP4s → Drive, plus both retention rules. (Only
  ONE machine should run the Drive-upload + retention jobs.)
- **Recording PC** — `config.samples\recording-pc.config.json`: just
  MKV → NAS.

Things to verify in the config:

- `Source` of the MKV job — wherever OBS writes recordings on that PC.
- NAS paths use UNC (`\\10.1.10.143\Public\...`) instead of `Z:` on purpose —
  mapped drive letters aren't reliable inside scheduled tasks.
- `J:\Thrive\Sermon Videos` as the MP4 source matches where the FileCopy tool
  lands finals today — adjust if that moves.
- Retention is `"MaxAgeDays": 365` on `*.mkv` only. Deleting is destructive:
  run with `-DryRun` after any retention change.

### 5. Test, then install the task

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\MediaSync.ps1 -DryRun   # shows every copy/move/delete it WOULD do
powershell -NoProfile -ExecutionPolicy Bypass -File .\MediaSync.ps1           # real pass
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DriveSyncTask.ps1
```

The task runs every 10 minutes (change with `-IntervalMinutes`), hidden,
skipping a pass if the previous one is still uploading. Remove with
`Uninstall-DriveSyncTask.ps1`.

## Notes / gotchas

- **Quota:** Google caps uploads at 750 GB/day per account — far above weekly
  volume.
- **Key rotation:** if the service-account key is ever revoked, generate a
  new JSON key and replace `C:\ProgramData\Thrive\gdrive-sa.json`; nothing
  else changes.
- **Both PCs write to the same NAS folder.** OBS filenames are timestamped so
  collisions are effectively impossible; if a duplicate name ever occurs the
  existing file is never overwritten (rclone skips identical files; the log
  will show it).
- **Retention grace:** Drive-side deletes sit in the Shared Drive trash for
  30 days before Google purges them; NAS-side deletes are immediate.
