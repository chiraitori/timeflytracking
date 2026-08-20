param(
    [string]$outputPath = "TimeFly.App/Assets/otd_tablets.json"
)

$tempDir = Join-Path $env:TEMP "otd-sparse-sync"
if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }

Write-Host "Cloning OpenTabletDriver Configurations..."
git clone --depth 1 --filter=blob:none --sparse https://github.com/OpenTabletDriver/OpenTabletDriver.git $tempDir
git -C $tempDir sparse-checkout set OpenTabletDriver.Configurations/Configurations

$configRoot = "$tempDir\OpenTabletDriver.Configurations\Configurations"
$files = Get-ChildItem -Path $configRoot -Filter "*.json" -Recurse

Write-Host "Processing $($files.Count) tablet configuration files..."

$map = @{}
$tabletCount = 0

foreach ($file in $files) {
    try {
        $json = Get-Content $file.FullName -Raw | ConvertFrom-Json
        if ($null -eq $json.Name) { continue }
        
        $name = $json.Name
        $manufacturer = if ($json.Manufacturer) { $json.Manufacturer } else { $file.Directory.Name }
        $maxPressure = if ($json.Specifications -and $json.Specifications.Pen -and $json.Specifications.Pen.MaxPressure) { [int]$json.Specifications.Pen.MaxPressure } else { 8191 }
        
        # Extract VID/PID from DigitizerIdentifiers
        if ($json.DigitizerIdentifiers) {
            foreach ($id in $json.DigitizerIdentifiers) {
                if ($id.VendorID -and $id.ProductID) {
                    $vidHex = ([int]$id.VendorID).ToString("X4")
                    $pidHex = ([int]$id.ProductID).ToString("X4")
                    $key = $vidHex + "_" + $pidHex
                    
                    $map[$key] = @{
                        name = $name
                        manufacturer = $manufacturer
                        raw_name = $name
                        vid = $vidHex
                        pid = $pidHex
                        input_report_length = if ($id.InputReportLength) { [int]$id.InputReportLength } else { 10 }
                        max_pressure = $maxPressure
                    }
                }
            }
        }
        elseif ($json.Attributes -and $json.Attributes.VendorID -and $json.Attributes.ProductID) {
            $vidHex = ([int]$json.Attributes.VendorID).ToString("X4")
            $pidHex = ([int]$json.Attributes.ProductID).ToString("X4")
            $key = $vidHex + "_" + $pidHex
            
            $map[$key] = @{
                name = $name
                manufacturer = $manufacturer
                raw_name = $name
                vid = $vidHex
                pid = $pidHex
                input_report_length = 10
                max_pressure = $maxPressure
            }
        }
        
        $tabletCount++
    }
    catch { }
}

# Preserve Supplementary models
$supplementary = @{
    "28BD_2904" = @{ name = "XP-Pen Deco 640"; manufacturer = "XP-Pen"; vid = "28BD"; pid = "2904"; max_pressure = 16384 }
    "28BD_0905" = @{ name = "XP-Pen Deco 640 (IT640)"; manufacturer = "XP-Pen"; vid = "28BD"; pid = "0905"; max_pressure = 16384 }
    "28BD_0935" = @{ name = "XP-Pen Deco Pro (Gen 2)"; manufacturer = "XP-Pen"; vid = "28BD"; pid = "0935"; max_pressure = 16384 }
    "28BD_0936" = @{ name = "XP-Pen Artist Pro 16 (Gen 2)"; manufacturer = "XP-Pen"; vid = "28BD"; pid = "0936"; max_pressure = 16384 }
}

foreach ($key in $supplementary.Keys) {
    $map[$key] = $supplementary[$key]
}

$outputObj = @{
    vid_pid_map = $map
    manufacturers = @{}
    tablet_count = $map.Count
    updated_at = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}

$jsonOutput = $outputObj | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText($outputPath, $jsonOutput, [System.Text.Encoding]::UTF8)

Write-Host "Successfully generated $outputPath with $($map.Count) tablet definitions!"
Remove-Item -Recurse -Force $tempDir
