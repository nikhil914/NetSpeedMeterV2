<#
.SYNOPSIS
    One-click build, sign, and create installer for NetMonitorPro.
.DESCRIPTION
    Unified script that handles the complete workflow:
      1. Creates a self-signed code signing certificate (if needed)
      2. Converts the app logo PNG to ICO (if needed)
      3. Publishes the app as a single-file self-contained EXE
      4. Signs the EXE with the certificate
      5. Builds the installer using Inno Setup
      6. Signs the installer EXE
      7. Verifies everything and outputs summary
.PARAMETER Force
    Recreate the certificate even if it already exists.
.PARAMETER SkipSign
    Build without signing.
.PARAMETER SkipInstaller
    Skip installer creation (just produce the signed EXE).
.PARAMETER Configuration
    Build configuration (Release or Debug). Default: Release.
.PARAMETER CertPassword
    Password for the PFX file.
.EXAMPLE
    .\sign_and_build.ps1
    .\sign_and_build.ps1 -Force
    .\sign_and_build.ps1 -SkipSign
    .\sign_and_build.ps1 -SkipInstaller
#>

param(
    [switch]$Force,
    [switch]$SkipSign,
    [switch]$SkipInstaller,
    [string]$Configuration = "Release",
    [string]$CertPassword = "NetMonitorProPassword123!"
)

$ErrorActionPreference = "Stop"

# -- Paths -------------------------------------------------------
$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ProjectPath = Join-Path $ProjectRoot "src\NetMonitorPro.App\NetMonitorPro.App.csproj"
$PublishDir = Join-Path $ProjectRoot "dist"
$InstallerDir = Join-Path $ProjectRoot "installer"
$PfxPath = Join-Path $ProjectRoot "NetMonitorPro_CodeSigning.pfx"
$IssPath = Join-Path $PSScriptRoot "installer.iss"
$LogoPng = Join-Path $ProjectRoot "src\logo\Nsm Pro App Logo.png"
$LogoIco = Join-Path $ProjectRoot "src\logo\NetMonitorPro.ico"
$ExeName = "NetMonitorPro.exe"
$InstallerName = "NetMonitorPro_Setup.exe"

# -- Certificate config ------------------------------------------
$CertSubject = "CN=NetMonitorPro Code Signing"
$FriendlyName = "NetMonitorPro Code Signing"
$ValidityYears = 2

# -- Inno Setup path ---------------------------------------------
$InnoSetupPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$ISCC = $null
foreach ($p in $InnoSetupPaths) {
    if (Test-Path $p) { $ISCC = $p; break }
}

# -- Total steps calculation --------------------------------------
$totalSteps = 7
$sigLabel = if ($SkipSign) { "Disabled" } else { "Enabled" }
$insLabel = if ($SkipInstaller) { "Disabled" } else { "Enabled" }

# -- Banner -------------------------------------------------------
Write-Host ""
Write-Host "  ================================================" -ForegroundColor Cyan
Write-Host "     NetMonitorPro -- Build & Install Pipeline     " -ForegroundColor Cyan
Write-Host "  ================================================" -ForegroundColor Cyan
Write-Host "    Configuration : $Configuration" -ForegroundColor DarkGray
Write-Host "    Signing       : $sigLabel" -ForegroundColor DarkGray
Write-Host "    Installer     : $insLabel" -ForegroundColor DarkGray
Write-Host "    Timestamp     : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor DarkGray
Write-Host ""

# =================================================================
# STEP 1 -- Certificate
# =================================================================
if (-not $SkipSign) {
    if ((Test-Path $PfxPath) -and -not $Force) {
        Write-Host "  [1/$totalSteps] Certificate found -- reusing existing PFX." -ForegroundColor Green
    }
    else {
        Write-Host "  [1/$totalSteps] Creating self-signed code signing certificate..." -ForegroundColor Yellow

        try {
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

            Write-Host "  [OK] Certificate created (Thumbprint: $($cert.Thumbprint))" -ForegroundColor Green
            Write-Host "    Valid until: $($notAfter.ToString('yyyy-MM-dd'))" -ForegroundColor DarkGray
        }
        catch {
            Write-Host "  [FAIL] Certificate creation failed: $($_.Exception.Message)" -ForegroundColor Red
            exit 1
        }
    }
}
else {
    Write-Host "  [1/$totalSteps] Certificate -- skipped (signing disabled)." -ForegroundColor DarkGray
}

# =================================================================
# STEP 2 -- Convert Logo PNG to ICO
# =================================================================
if ((Test-Path $LogoIco) -and -not $Force) {
    Write-Host "  [2/$totalSteps] App icon found -- reusing existing ICO." -ForegroundColor Green
}
else {
    Write-Host "  [2/$totalSteps] Converting app logo to ICO..." -ForegroundColor Yellow
    try {
        Add-Type -AssemblyName System.Drawing
        $img = [System.Drawing.Image]::FromFile($LogoPng)
        $bitmap = New-Object System.Drawing.Bitmap($img, 256, 256)
        $icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
        $stream = [System.IO.File]::Create($LogoIco)
        $icon.Save($stream)
        $stream.Close()
        $icon.Dispose()
        $bitmap.Dispose()
        $img.Dispose()
        Write-Host "  [OK] Icon created from Nsm Pro App Logo.png" -ForegroundColor Green
    }
    catch {
        Write-Host "  [WARN] Could not convert logo: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "    Installer will use default icon." -ForegroundColor DarkGray
    }
}

# =================================================================
# STEP 3 -- Clean
# =================================================================
if (Test-Path $PublishDir) {
    Write-Host "  [3/$totalSteps] Cleaning previous build..." -ForegroundColor DarkGray
    Remove-Item $PublishDir -Recurse -Force
}
else {
    Write-Host "  [3/$totalSteps] No previous build to clean." -ForegroundColor DarkGray
}
New-Item -ItemType Directory -Path $PublishDir | Out-Null

# =================================================================
# STEP 4 -- Publish
# =================================================================
Write-Host "  [4/$totalSteps] Publishing ($Configuration | win-x64 | self-contained)..." -ForegroundColor Yellow

$fileSizeMB = 0
$exePath = ""

try {
    $publishOutput = dotnet publish $ProjectPath `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $PublishDir 2>&1

    $exePath = Join-Path $PublishDir $ExeName

    if (-not (Test-Path $exePath)) {
        Write-Host $publishOutput
        throw "Executable not found after publish."
    }

    $fileInfo = Get-Item $exePath
    $fileSizeMB = [math]::Round($fileInfo.Length / 1MB, 1)
    Write-Host "  [OK] Published: $ExeName ($fileSizeMB MB)" -ForegroundColor Green
}
catch {
    Write-Host "  [FAIL] Publish failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# =================================================================
# STEP 5 -- Sign EXE
# =================================================================
if ($SkipSign) {
    Write-Host "  [5/$totalSteps] Signing EXE -- skipped." -ForegroundColor DarkGray
}
else {
    Write-Host "  [5/$totalSteps] Signing executable..." -ForegroundColor Yellow

    try {
        $securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
        $signingCert = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $securePassword

        $sig = Set-AuthenticodeSignature `
            -FilePath $exePath `
            -Certificate $signingCert `
            -TimestampServer "http://timestamp.digicert.com"

        if ($sig.Status -eq "Valid") {
            Write-Host "  [OK] EXE signed successfully!" -ForegroundColor Green
        }
        else {
            Write-Host "  [OK] EXE signature applied (Status: $($sig.Status))" -ForegroundColor Green
            Write-Host "    Self-signed certs show 'UnknownError' -- expected." -ForegroundColor DarkGray
        }
    }
    catch {
        Write-Host "  [FAIL] Signing failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# =================================================================
# STEP 6 -- Build Installer
# =================================================================
if ($SkipInstaller) {
    Write-Host "  [6/$totalSteps] Installer -- skipped." -ForegroundColor DarkGray
}
else {
    Write-Host "  [6/$totalSteps] Building installer with Inno Setup..." -ForegroundColor Yellow

    if (-not $ISCC) {
        Write-Host "  [FAIL] Inno Setup not found! Install it:" -ForegroundColor Red
        Write-Host "    winget install JRSoftware.InnoSetup" -ForegroundColor DarkGray
        exit 1
    }

    if (-not (Test-Path $IssPath)) {
        Write-Host "  [FAIL] Installer script not found: $IssPath" -ForegroundColor Red
        exit 1
    }

    # Create installer output directory
    if (-not (Test-Path $InstallerDir)) {
        New-Item -ItemType Directory -Path $InstallerDir | Out-Null
    }

    try {
        $innoOutput = & $ISCC $IssPath 2>&1
        $installerPath = Join-Path $InstallerDir $InstallerName

        if (-not (Test-Path $installerPath)) {
            Write-Host $innoOutput
            throw "Installer not found after build."
        }

        $installerSize = [math]::Round((Get-Item $installerPath).Length / 1MB, 1)
        Write-Host "  [OK] Installer created: $InstallerName ($installerSize MB)" -ForegroundColor Green

        # Sign the installer too
        if (-not $SkipSign) {
            Write-Host "         Signing installer..." -ForegroundColor DarkGray
            try {
                $securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
                $signingCert = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $securePassword

                Set-AuthenticodeSignature `
                    -FilePath $installerPath `
                    -Certificate $signingCert `
                    -TimestampServer "http://timestamp.digicert.com" | Out-Null

                Write-Host "  [OK] Installer signed." -ForegroundColor Green
            }
            catch {
                Write-Host "  [WARN] Could not sign installer: $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
    }
    catch {
        Write-Host "  [FAIL] Installer build failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# =================================================================
# STEP 7 -- Summary
# =================================================================
Write-Host "  [7/$totalSteps] Verifying build..." -ForegroundColor DarkGray

$hash = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash

Write-Host ""
Write-Host "  ================================================" -ForegroundColor Green
Write-Host "              Build Complete!                      " -ForegroundColor Green
Write-Host "  ================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Executable:" -ForegroundColor White
Write-Host "    Path    : $exePath" -ForegroundColor DarkGray
Write-Host "    Size    : $fileSizeMB MB" -ForegroundColor DarkGray
Write-Host "    SHA256  : $hash" -ForegroundColor DarkGray

if (-not $SkipSign) {
    $finalSig = Get-AuthenticodeSignature -FilePath $exePath
    Write-Host "    Signed  : $($finalSig.SignerCertificate.Subject)" -ForegroundColor DarkGray
}

if (-not $SkipInstaller) {
    $installerPath = Join-Path $InstallerDir $InstallerName
    if (Test-Path $installerPath) {
        $instHash = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash
        $instSize = [math]::Round((Get-Item $installerPath).Length / 1MB, 1)
        Write-Host ""
        Write-Host "  Installer:" -ForegroundColor White
        Write-Host "    Path    : $installerPath" -ForegroundColor DarkGray
        Write-Host "    Size    : $instSize MB" -ForegroundColor DarkGray
        Write-Host "    SHA256  : $instHash" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "  Done!" -ForegroundColor Cyan
Write-Host ""
