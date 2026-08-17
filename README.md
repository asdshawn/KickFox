<p align="center">
  <img src="icon.ico" width="128" height="128">
</p>

<h1 align="center">KickFox</h1>
<p align="center">A smart, automated Wi-Fi monitor & auto-reconnect utility for Windows.</p>

## Overview
KickFox is a lightweight, background Windows application designed to keep your internet connection alive. If your Wi-Fi drops or gets stuck in a loop, KickFox automatically detects the outage and restarts your wireless adapters to get you back online.

## Features
- **Auto-Reconnect**: Pings multiple targets (e.g. 8.8.8.8) to verify connectivity.
- **Background Execution**: Runs silently in the system tray.
- **Smart Retries**: Configurable interval and max retries to prevent infinite restart loops.
- **Auto-Update**: Built-in GitHub Releases auto-updater (`Updater.exe`).

## Installation
1. Go to the [Releases](../../releases/latest) page.
2. Download `KickFoxInstaller.exe`.
3. Run the installer to register the background service and create a desktop shortcut.

## Auto-Update Architecture
KickFox uses GitHub Releases as its update server. It periodically checks the latest tag via the GitHub API. When a new version is found, it downloads `Update.zip` and launches `Updater.exe` to seamlessly replace the executable files.

## Build from Source
Run `Build.ps1` in PowerShell to compile the C# source files via `csc.exe`.

## License
MIT License.
