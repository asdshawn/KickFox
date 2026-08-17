$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { Write-Error "Compiler not found."; exit 1 }

Write-Host "Compiling KickFox.exe..."
& $csc /nologo /target:winexe /win32icon:icon.ico /out:KickFox.exe KickFoxApp.cs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Compiling KickFoxInstaller.exe..."
& $csc /nologo /target:winexe /win32icon:icon.ico /out:KickFoxInstaller.exe KickFoxInstaller.cs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Compiling KickFoxUninstaller.exe..."
& $csc /nologo /target:winexe /win32icon:icon.ico /out:KickFoxUninstaller.exe KickFoxUninstaller.cs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Compiling Updater.exe..."
& $csc /nologo /target:winexe /win32icon:icon.ico /r:System.IO.Compression.FileSystem.dll /out:Updater.exe Updater.cs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Build Successful!"
