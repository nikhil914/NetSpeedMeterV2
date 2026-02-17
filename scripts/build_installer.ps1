<#
.SYNOPSIS
    Publishes and optionally signs the NetMonitorPro executable.
.PARAMETER SkipSign
    Build without signing the executable.
.PARAMETER Configuration
    Build configuration. Default: Release.
.EXAMPLE
    .\build_installer.ps1
    .\build_installer.ps1 -SkipSign
    .\build_installer.ps1 -Configuration Debug
#>

param(
    [switch]$SkipSign,
    [string]$Configuration = "Release",
    [string]$CertPassword = "NetMonitorProPassword123!"
)

$ErrorActionPreference = "Stop"

$ProjectPath = Join-Path $PSScriptRoot "..\src\NetMonitorPro.App\NetMonitorPro.App.csproj"
$PublishDir = Join-Path $PSScriptRoot "..\dist"
$CertPath = Join-Path $PSScriptRoot "..\NetMonitorPro_CodeSigning.pfx"
$ExeName = "NetMonitorPro.exe"

Write-Host ""
Write-Host "  ========================================" -ForegroundColor Cyan
Write-Host "       NetMonitorPro Build & Sign         " -ForegroundColor Cyan
Write-Host "  ========================================" -ForegroundColor Cyan
Write-Host ""

# Clean
if (Test-Path $PublishDir) {
    Write-Host "  [1/4] Cleaning previous build..." -ForegroundColor DarkGray
    Remove-Item $PublishDir -Recurse -Force
}
else {
    Write-Host "  [1/4] No previous build to clean." -ForegroundColor DarkGray
}
New-Item -ItemType Directory -Path $PublishDir | Out-Null

# Publish
Write-Host "  [2/4] Publishing ($Configuration | win-x64 | self-contained)..." -ForegroundColor Yellow

try {
    dotnet publish $ProjectPath `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $PublishDir 2>&1 | Out-Null

    $exePath = Join-Path $PublishDir $ExeName

    if (-not (Test-Path $exePath)) {
        throw "Executable not found after publish: $exePath"
    }

    $fileInfo = Get-Item $exePath
    $fileSizeMB = [math]::Round($fileInfo.Length / 1MB, 1)
    Write-Host "  [OK] Published: $ExeName ($fileSizeMB MB)" -ForegroundColor Green
}
catch {
    Write-Host "  [FAIL] Publish failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Sign
if ($SkipSign) {
    Write-Host "  [3/4] Signing skipped (--SkipSign)." -ForegroundColor DarkGray
}
else {
    Write-Host "  [3/4] Signing executable..." -ForegroundColor Yellow

    if (-not (Test-Path $CertPath)) {
        Write-Host ""
        Write-Host "  [FAIL] Certificate not found: $CertPath" -ForegroundColor Red
        Write-Host "    Run .\create_cert.ps1 first, or use -SkipSign." -ForegroundColor DarkGray
        Write-Host ""
        exit 1
    }

    try {
        $securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
        $cert = Import-PfxCertificate -FilePath $CertPath -CertStoreLocation Cert:\CurrentUser\My -Password $securePassword

        $sig = Set-AuthenticodeSignature `
            -FilePath $exePath `
            -Certificate $cert `
            -TimestampServer "http://timestamp.digicert.com"

        if ($sig.Status -eq "Valid") {
            Write-Host "  [OK] Signed by: $($sig.SignerCertificate.Subject)" -ForegroundColor Green
        }
        else {
            Write-Host "  [OK] Signature applied (Status: $($sig.Status))" -ForegroundColor Yellow
            Write-Host "    Self-signed certs show 'UnknownError' -- this is normal." -ForegroundColor DarkGray
        }
    }
    catch {
        Write-Host "  [FAIL] Signing failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Summary
Write-Host "  [4/4] Generating file hash..." -ForegroundColor DarkGray
$hash = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash

Write-Host ""
Write-Host "  ========================================" -ForegroundColor Green
Write-Host "           Build Complete!                " -ForegroundColor Green
Write-Host "  ========================================" -ForegroundColor Green
Write-Host "    Output : $exePath" -ForegroundColor DarkGray
Write-Host "    Size   : $fileSizeMB MB" -ForegroundColor DarkGray
Write-Host "    SHA256 : $hash" -ForegroundColor DarkGray
Write-Host ""
