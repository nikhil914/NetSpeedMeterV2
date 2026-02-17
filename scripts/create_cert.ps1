<#
.SYNOPSIS
    Creates a self-signed code signing certificate for NetMonitorPro.
.PARAMETER Force
    Recreate the certificate even if PFX already exists.
.PARAMETER CertPassword
    Password for the PFX file.
.EXAMPLE
    .\create_cert.ps1
    .\create_cert.ps1 -Force
#>

param(
    [switch]$Force,
    [string]$CertPassword = "NetMonitorProPassword123!"
)

$ErrorActionPreference = "Stop"

$CertSubject = "CN=NetMonitorPro Code Signing"
$FriendlyName = "NetMonitorPro Code Signing"
$ValidityYears = 2
$PfxPath = Join-Path $PSScriptRoot "..\NetMonitorPro_CodeSigning.pfx"

if ((Test-Path $PfxPath) -and -not $Force) {
    Write-Host ""
    Write-Host "  Certificate already exists: $PfxPath" -ForegroundColor Yellow
    Write-Host "  Use -Force to recreate it." -ForegroundColor DarkGray
    Write-Host ""
    exit 0
}

try {
    Write-Host ""
    Write-Host "  Creating self-signed code signing certificate..." -ForegroundColor Cyan

    $securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
    $notAfter = (Get-Date).AddYears($ValidityYears)

    $cert = New-SelfSignedCertificate `
        -CertStoreLocation Cert:\CurrentUser\My `
        -Subject $CertSubject `
        -FriendlyName $FriendlyName `
        -Type CodeSigningCert `
        -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider" `
        -NotAfter $notAfter

    Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $securePassword | Out-Null

    Write-Host ""
    Write-Host "  [OK] Certificate created successfully!" -ForegroundColor Green
    Write-Host "    Subject     : $CertSubject" -ForegroundColor DarkGray
    Write-Host "    Thumbprint  : $($cert.Thumbprint)" -ForegroundColor DarkGray
    Write-Host "    Valid until : $($notAfter.ToString('yyyy-MM-dd'))" -ForegroundColor DarkGray
    Write-Host "    PFX path    : $PfxPath" -ForegroundColor DarkGray
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "  [FAIL] Failed to create certificate!" -ForegroundColor Red
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    exit 1
}
