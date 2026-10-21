<p align="center">
  <img src="icon.ico" width="128" height="128" alt="KickFox logo">
</p>

<h1 align="center">KickFox</h1>
<p align="center">A lightweight Windows tray utility that detects a dead internet connection and automatically restarts your Wi-Fi adapter.</p>

## Features
- **Connectivity check** – pings a configurable, ordered list of targets (default `8.8.8.8`, `1.1.1.1`, `168.95.1.1`).
- **Auto-kick** – if every target fails, KickFox disables and re-enables your Wi-Fi adapter(s).
- **Retry limit** – after the configured number of failed kicks it pauses for 30 minutes instead of looping forever.
- **Animated tray icon** – normal / kicking / failed states, with live ping in the tooltip.
- **Signed auto-update** – checks GitHub Releases and only installs packages signed with the KickFox release key.

## Installation
1. Open the [latest release](../../releases/latest).
2. Download **`KickFoxInstaller.exe`** (this single file is all you need).
3. Run it and click **Install**. KickFox is installed to `C:\Program Files\KickFox` and starts automatically at logon.

> **Windows SmartScreen:** KickFox is not code-signed yet, so Windows may show *"Windows protected your PC"*. Click **More info → Run anyway**. You can verify the source and build it yourself (see below).

> KickFox needs administrator rights because enabling/disabling network adapters is a privileged operation.

Uninstall from **Settings → Apps** (or *Programs and Features*) like any other program.

## How auto-update works
1. KickFox queries `https://api.github.com/repos/asdshawn/KickFox/releases/latest` (interval configurable in Settings).
2. If the release tag (e.g. `v1.2.0`) is **newer** than the installed version, it downloads `Update.zip` and `Update.zip.sig`.
3. The RSA-SHA256 signature is verified against the public key embedded in KickFox (`UpdatePublicKey.xml`). Unsigned or tampered packages are rejected.
4. `Updater.exe` verifies the signature again, backs up the current files, installs the new ones, rolls back on failure, and restarts KickFox.

Logs: `C:\ProgramData\KickFox\monitor_log.txt` and `updater_log.txt`.

## Building from source
Requirements: Windows with .NET Framework 4.x (the built-in `csc.exe` is used; no Visual Studio needed).

```powershell
# One-time: create your own signing key pair (only needed if you publish releases)
.\New-SigningKey.ps1

# Build everything
.\Build.ps1
```

Output:

| Path | Purpose |
|---|---|
| `dist\release\KickFoxInstaller.exe` | Standalone installer |
| `dist\release\Update.zip` + `Update.zip.sig` | Signed auto-update package |
| `dist\repo\` | Source files that belong in this repository |

## Publishing a release (maintainers)
1. Bump `Version` / `AssemblyVersion` in `AppVersion.cs`.
2. Run `.\Build.ps1` (requires the private key at `%USERPROFILE%\.kickfox\signing_private.xml`).
3. Create a GitHub release with tag **`v<Version>`** and upload the three files from `dist\release\`.
   Asset names must stay exactly `KickFoxInstaller.exe`, `Update.zip`, `Update.zip.sig`.

## License
[MIT](LICENSE)