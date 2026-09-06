<#
.SYNOPSIS
    BIM-Bot License Key Generator — Admin tool for Hassan.

.DESCRIPTION
    Generates and activates a license key for a BIM-Bot user by calling
    the Firebase Cloud Function 'generateLicenseKey'.

.PARAMETER MachineId
    The user's Machine ID (e.g. BIM-8F42-9B1A-77C0).

.PARAMETER Username
    The user's Windows username.

.PARAMETER Email
    The user's email address (optional).

.EXAMPLE
    .\generate-key.ps1 -MachineId "BIM-8F42-9B1A-77C0" -Username "john_doe" -Email "john@company.com"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$MachineId,

    [Parameter(Mandatory = $true)]
    [string]$Username,

    [Parameter(Mandatory = $false)]
    [string]$Email = ""
)

$ErrorActionPreference = "Stop"

# ── Configuration ──
$FirebaseUrl = "https://europe-west1-revit-mcp10.cloudfunctions.net/generateLicenseKey"
# The admin secret is never stored in the repo. Set it once per shell:
#   $env:BIMBOT_ADMIN_SECRET = "<the secret from Firebase Secret Manager>"
$AdminSecret = $env:BIMBOT_ADMIN_SECRET
if ([string]::IsNullOrWhiteSpace($AdminSecret)) {
    Write-Host "BIMBOT_ADMIN_SECRET is not set." -ForegroundColor Red
    Write-Host 'Set it first:  $env:BIMBOT_ADMIN_SECRET = "<secret>"' -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║       BIM-Bot License Key Generator          ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Machine ID : $MachineId" -ForegroundColor Yellow
Write-Host "  Username   : $Username" -ForegroundColor Yellow
Write-Host "  Email      : $(if ($Email) { $Email } else { '(not provided)' })" -ForegroundColor Yellow
Write-Host ""
Write-Host "Generating license key..." -ForegroundColor Gray

try {
    $body = @{
        machineId   = $MachineId
        username    = $Username
        email       = $Email
        adminSecret = $AdminSecret
    } | ConvertTo-Json -Compress

    $response = Invoke-RestMethod -Uri $FirebaseUrl -Method POST `
        -ContentType "application/json" `
        -Body $body `
        -TimeoutSec 30

    if ($response.success -eq $true) {
        Write-Host ""
        Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Green
        Write-Host "║  ✅ License Key Generated Successfully!       ║" -ForegroundColor Green
        Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Green
        Write-Host ""
        Write-Host "  License Key: $($response.licenseKey)" -ForegroundColor White -BackgroundColor DarkGreen
        Write-Host ""
        Write-Host "  Send this key to the user: $Username" -ForegroundColor Gray
        Write-Host ""

        # Copy to clipboard
        $response.licenseKey | Set-Clipboard
        Write-Host "  📋 Key copied to clipboard!" -ForegroundColor Cyan
    }
    else {
        Write-Host ""
        Write-Host "  ❌ Failed: $($response | ConvertTo-Json -Compress)" -ForegroundColor Red
    }
}
catch {
    Write-Host ""
    Write-Host "  ❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Make sure:" -ForegroundColor Yellow
    Write-Host "    1. Firebase Cloud Functions are deployed" -ForegroundColor Yellow
    Write-Host "    2. You have internet connectivity" -ForegroundColor Yellow
    Write-Host "    3. The admin secret is correct" -ForegroundColor Yellow
}

Write-Host ""
