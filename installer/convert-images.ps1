# Convert PNG wizard images to properly-sized BMP files for Inno Setup
Add-Type -AssemblyName System.Drawing

$assetsDir = "$PSScriptRoot\assets"
$brainDir = "C:\Users\user\.gemini\antigravity\brain\60107eb6-6f8c-4def-a2c7-84d3e7b703bc"

# --- WizardImageFile: 164x314 BMP (sidebar on Welcome/Finish pages) ---
$sidebarSrc = "$brainDir\wizard_sidebar_1774854383293.png"
if (Test-Path $sidebarSrc) {
    $img = [System.Drawing.Image]::FromFile($sidebarSrc)
    $bmp = New-Object System.Drawing.Bitmap(164, 314)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($img, 0, 0, 164, 314)
    $bmp.Save("$assetsDir\WizardImageFile.bmp", [System.Drawing.Imaging.ImageFormat]::Bmp)
    $g.Dispose()
    $bmp.Dispose()
    $img.Dispose()
    Write-Host "[OK] WizardImageFile.bmp created (164x314)"
} else {
    Write-Host "[SKIP] Sidebar source not found, using existing BMP"
}

# --- WizardSmallImageFile: 55x55 BMP (header on inner pages) ---
$headerSrc = "$brainDir\wizard_header_1774854401308.png"
if (Test-Path $headerSrc) {
    $img = [System.Drawing.Image]::FromFile($headerSrc)
    $bmp = New-Object System.Drawing.Bitmap(55, 55)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($img, 0, 0, 55, 55)
    $bmp.Save("$assetsDir\WizardSmallImageFile.bmp", [System.Drawing.Imaging.ImageFormat]::Bmp)
    $g.Dispose()
    $bmp.Dispose()
    $img.Dispose()
    Write-Host "[OK] WizardSmallImageFile.bmp created (55x55)"
} else {
    Write-Host "[SKIP] Header source not found, using existing BMP"
}

# --- Also convert icon.png to proper ICO if needed ---
# The existing revitmcp.ico already exists (41KB, multi-size), so we keep it
Write-Host ""
Write-Host "Assets ready for Inno Setup compilation!"
