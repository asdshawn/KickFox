<#
.SYNOPSIS
  Builds KickFox and produces ready-to-upload release assets.

.OUTPUTS
  dist\release\KickFoxInstaller.exe   Standalone installer (all program files embedded)
  dist\release\Update.zip             Auto-update package
  dist\release\Update.zip.sig         RSA-SHA256 signature of Update.zip
  dist\repo\                          Exactly the files that belong in the GitHub repository
#>
param(
    [string]$PrivateKeyPath = (Join-Path $env:USERPROFILE ".kickfox\signing_private.xml")
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }
if (-not (Test-Path "UpdatePublicKey.xml")) { throw "UpdatePublicKey.xml is missing. Run .\New-SigningKey.ps1 once first." }

$version = [regex]::Match((Get-Content AppVersion.cs -Raw), 'Version = "([^"]+)"').Groups[1].Value
Write-Host "Building KickFox v$version" -ForegroundColor Cyan

function Invoke-Csc([string[]]$cscArgs) {
    & $csc /nologo /optimize+ @cscArgs
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $($cscArgs[-1])" }
}

$icons = "/res:IconNormal.ico", "/res:IconKickA.ico", "/res:IconKickB.ico", "/res:IconFatal.ico"

Write-Host "Compiling KickFox.exe..."
Invoke-Csc (@("/target:winexe", "/win32icon:icon.ico", "/r:System.Web.Extensions.dll", "/res:UpdatePublicKey.xml") + $icons + @("/out:KickFox.exe", "AppVersion.cs", "UpdateVerifier.cs", "KickFoxApp.cs"))

Write-Host "Compiling KickFoxUninstaller.exe..."
Invoke-Csc @("/target:winexe", "/win32icon:icon.ico", "/out:KickFoxUninstaller.exe", "AppVersion.cs", "KickFoxUninstaller.cs")

Write-Host "Compiling Updater.exe..."
Invoke-Csc @("/target:winexe", "/win32icon:icon.ico", "/r:System.IO.Compression.dll", "/r:System.IO.Compression.FileSystem.dll", "/res:UpdatePublicKey.xml", "/out:Updater.exe", "AppVersion.cs", "UpdateVerifier.cs", "Updater.cs")

Write-Host "Compiling KickFoxInstaller.exe (with embedded payload)..."
Invoke-Csc @("/target:winexe", "/win32icon:icon.ico", "/res:KickFox.exe,payload.KickFox.exe", "/res:KickFoxUninstaller.exe,payload.KickFoxUninstaller.exe", "/res:Updater.exe,payload.Updater.exe", "/out:KickFoxInstaller.exe", "AppVersion.cs", "KickFoxInstaller.cs")

# ── Release assets ───────────────────────────────────────────────
$dist = Join-Path $PSScriptRoot "dist"
$release = Join-Path $dist "release"
$staging = Join-Path $dist "update_staging"
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $release, $staging | Out-Null

Copy-Item KickFoxInstaller.exe $release
Copy-Item KickFox.exe, KickFoxUninstaller.exe $staging
# Shipped as Updater.new.exe: a running Updater.exe can't overwrite itself (old v1.0.0 updaters skip
# "Updater.exe" entirely), so KickFox.exe swaps the new updater in on its next start.
Copy-Item Updater.exe (Join-Path $staging "Updater.new.exe")

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $release "Update.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($staging, $zip)
Remove-Item $staging -Recurse -Force

# ── Sign ─────────────────────────────────────────────────────────
if (Test-Path $PrivateKeyPath) {
    $csp = New-Object System.Security.Cryptography.CspParameters 24
    $rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider -ArgumentList $csp
    $rsa.PersistKeyInCsp = $false
    try {
        $rsa.FromXmlString((Get-Content $PrivateKeyPath -Raw))
        $publicXml = (Get-Content UpdatePublicKey.xml -Raw).Trim()
        if ($rsa.ToXmlString($false) -ne $publicXml) { throw "The private key does not match UpdatePublicKey.xml." }
        $bytes = [IO.File]::ReadAllBytes($zip)
        $sig = $rsa.SignData($bytes, [object]"SHA256")
        if (-not $rsa.VerifyData($bytes, [object]"SHA256", $sig)) { throw "Self-verification of the signature failed." }
        [IO.File]::WriteAllText("$zip.sig", [Convert]::ToBase64String($sig))
        Write-Host "Update.zip signed." -ForegroundColor Green
    }
    finally { $rsa.Dispose() }
}
else {
    Write-Warning "Private key not found at $PrivateKeyPath. Update.zip.sig was NOT created; clients will refuse this update."
}

# ── Repository snapshot (for uploading via the GitHub web UI) ────
$repoFiles = @(
    ".gitignore", "LICENSE", "README.md",
    "Build.ps1", "New-SigningKey.ps1",
    "AppVersion.cs", "UpdateVerifier.cs", "KickFoxApp.cs", "KickFoxInstaller.cs", "KickFoxUninstaller.cs", "Updater.cs",
    "UpdatePublicKey.xml",
    "icon.ico", "IconNormal.ico", "IconKickA.ico", "IconKickB.ico", "IconFatal.ico"
)
$repo = Join-Path $dist "repo"
New-Item -ItemType Directory -Force $repo | Out-Null
foreach ($file in $repoFiles) {
    if (-not (Test-Path $file)) { throw "Repository file missing: $file" }
    Copy-Item $file $repo
}

Write-Host ""
Write-Host "Build successful: KickFox v$version" -ForegroundColor Cyan
Write-Host "  Release assets : $release  (tag: v$version)"
Write-Host "  Repo snapshot  : $repo"