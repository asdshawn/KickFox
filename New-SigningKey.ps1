<#
.SYNOPSIS
  Generates the RSA key pair used to sign KickFox update packages.

.DESCRIPTION
  - Private key -> %USERPROFILE%\.kickfox\signing_private.xml  (NEVER commit or share this file; back it up offline)
  - Public key  -> .\UpdatePublicKey.xml                       (committed; embedded into KickFox.exe and Updater.exe)

  If the private key is lost, existing installations can no longer receive auto-updates
  and users will have to reinstall manually from GitHub Releases.
#>
param(
    [string]$PrivateKeyPath = (Join-Path $env:USERPROFILE ".kickfox\signing_private.xml"),
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if ((Test-Path $PrivateKeyPath) -and -not $Force) {
    Write-Error "A private key already exists at $PrivateKeyPath. Re-generating it breaks auto-update for every existing install. Use -Force only if you really mean it."
    exit 1
}

$csp = New-Object System.Security.Cryptography.CspParameters 24   # PROV_RSA_AES (supports SHA-256)
$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider -ArgumentList 3072, $csp
$rsa.PersistKeyInCsp = $false
try {
    New-Item -ItemType Directory -Force -Path (Split-Path $PrivateKeyPath) | Out-Null
    [IO.File]::WriteAllText($PrivateKeyPath, $rsa.ToXmlString($true))
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot "UpdatePublicKey.xml"), $rsa.ToXmlString($false))
}
finally {
    $rsa.Dispose()
}

Write-Host "Private key: $PrivateKeyPath  (back this up somewhere safe, never upload it)" -ForegroundColor Yellow
Write-Host "Public key : $(Join-Path $PSScriptRoot 'UpdatePublicKey.xml')" -ForegroundColor Green