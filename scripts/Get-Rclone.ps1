param(
    [string]$Version = "current",
    [ValidateSet("amd64", "386", "arm64")]
    [string]$Architecture = "amd64",
    [string]$OutputDirectory = "third_party/rclone"
)

$ErrorActionPreference = "Stop"

$targetDirectory = Join-Path (Get-Location) $OutputDirectory
$tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("rclone-download-" + [Guid]::NewGuid())
$zipPath = Join-Path $tempDirectory "rclone.zip"

New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $tempDirectory | Out-Null

try {
    $url = if ($Version -eq "current") {
        "https://downloads.rclone.org/rclone-current-windows-$Architecture.zip"
    } else {
        "https://downloads.rclone.org/v$Version/rclone-v$Version-windows-$Architecture.zip"
    }

    Write-Host "Downloading rclone: $url"
    Invoke-WebRequest -Uri $url -OutFile $zipPath

    Expand-Archive -Path $zipPath -DestinationPath $tempDirectory -Force
    $rclone = Get-ChildItem -Path $tempDirectory -Filter "rclone.exe" -Recurse | Select-Object -First 1
    if ($null -eq $rclone) {
        throw "Downloaded archive did not contain rclone.exe"
    }

    Copy-Item -Path $rclone.FullName -Destination (Join-Path $targetDirectory "rclone.exe") -Force
    $versionText = & (Join-Path $targetDirectory "rclone.exe") version | Select-Object -First 1
    Set-Content -Path (Join-Path $targetDirectory "VERSION") -Value $versionText -Encoding UTF8

    Write-Host "$versionText copied to $targetDirectory"
}
finally {
    Remove-Item -Path $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
