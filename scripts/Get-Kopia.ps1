param(
    [string]$Version = "0.23.0",
    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64",
    [string]$OutputDirectory = "third_party/kopia"
)

$ErrorActionPreference = "Stop"

$repo = "kopia/kopia"
$tag = "v$Version"
$assetPattern = "kopia-$Version-windows-$Architecture.zip"
$releaseUrl = "https://api.github.com/repos/$repo/releases/tags/$tag"

Write-Host "Fetching Kopia release metadata: $tag"
$release = Invoke-RestMethod -Uri $releaseUrl -Headers @{
    "Accept" = "application/vnd.github+json"
    "User-Agent" = "Kopia-WinUI3"
}

$asset = $release.assets | Where-Object { $_.name -eq $assetPattern } | Select-Object -First 1
if ($null -eq $asset) {
    $available = ($release.assets | ForEach-Object { $_.name }) -join "`n  "
    throw "Could not find asset '$assetPattern'. Available assets:`n  $available"
}

$root = Resolve-Path "."
$targetDirectory = Join-Path $root $OutputDirectory
$downloadDirectory = Join-Path $root "artifacts/downloads"
$zipPath = Join-Path $downloadDirectory $asset.name
$extractDirectory = Join-Path $downloadDirectory ([IO.Path]::GetFileNameWithoutExtension($asset.name))

New-Item -ItemType Directory -Force -Path $targetDirectory, $downloadDirectory | Out-Null

Write-Host "Downloading $($asset.browser_download_url)"
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath

if (Test-Path $extractDirectory) {
    Remove-Item -LiteralPath $extractDirectory -Recurse -Force
}

Expand-Archive -Path $zipPath -DestinationPath $extractDirectory

$kopia = Get-ChildItem -Path $extractDirectory -Filter "kopia.exe" -Recurse | Select-Object -First 1
if ($null -eq $kopia) {
    throw "Downloaded archive did not contain kopia.exe"
}

Copy-Item -Path $kopia.FullName -Destination (Join-Path $targetDirectory "kopia.exe") -Force

$versionFile = Join-Path $targetDirectory "VERSION"
Set-Content -Path $versionFile -Value $Version -Encoding UTF8

Write-Host "Kopia $Version copied to $targetDirectory"
