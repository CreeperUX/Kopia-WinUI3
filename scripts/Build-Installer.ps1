param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64",

    [string]$Version = "1.0.1",
    [string]$Publisher = "CreeperUX"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "src\RcloneWinUI3\RcloneWinUI3.csproj"
$setupProjectPath = Join-Path $repoRoot "installer\bootstrapper\RcloneWinUI3.Setup.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\RcloneWinUI3-$Runtime"
$installerRoot = Join-Path $repoRoot "artifacts\installer"
$bundleDir = Join-Path $installerRoot "bundle"
$payloadZip = Join-Path $bundleDir "payload.zip"
$setupPath = Join-Path $installerRoot "RcloneWinUI3-Setup-$Runtime.exe"

$platform = switch ($Runtime) {
    "win-x64" { "x64" }
    "win-x86" { "x86" }
    "win-arm64" { "ARM64" }
}

if (-not (Test-Path -LiteralPath $setupProjectPath)) {
    throw "Missing installer project: $setupProjectPath"
}

Get-Process -Name "RcloneWinUI3" -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $bundleDir) {
    Remove-Item -LiteralPath $bundleDir -Recurse -Force
}

if (Test-Path -LiteralPath $installerRoot) {
    Get-ChildItem -LiteralPath $installerRoot -Exclude "bundle" | Remove-Item -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publishDir, $bundleDir, $installerRoot | Out-Null

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir `
    -p:Platform=$platform `
    -p:Version=$Version `
    -p:FileVersion=$Version `
    -p:AssemblyVersion=$Version

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$publishedExe = Join-Path $publishDir "RcloneWinUI3.exe"
$publishedRclone = Join-Path $publishDir "bin\rclone.exe"
$publishedPri = Join-Path $publishDir "RcloneWinUI3.pri"

if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Publish output is missing RcloneWinUI3.exe"
}

if (-not (Test-Path -LiteralPath $publishedRclone)) {
    throw "Publish output is missing bundled bin\rclone.exe"
}

if (-not (Test-Path -LiteralPath $publishedPri)) {
    $generatedPri = Get-ChildItem `
        -LiteralPath (Join-Path $repoRoot "src\RcloneWinUI3\bin") `
        -Recurse `
        -Filter "RcloneWinUI3.pri" |
        Where-Object { $_.FullName -like "*\$Configuration\*" -and $_.FullName -like "*\$Runtime\*" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $generatedPri) {
        throw "Publish output is missing RcloneWinUI3.pri and no generated PRI file was found"
    }

    Copy-Item -LiteralPath $generatedPri.FullName -Destination $publishedPri -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $payloadZip -Force

dotnet publish $setupProjectPath `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -o $installerRoot `
    -p:Platform=$platform `
    -p:PayloadZip=$payloadZip `
    -p:Version=$Version `
    -p:FileVersion=$Version `
    -p:AssemblyVersion=$Version

if ($LASTEXITCODE -ne 0) {
    throw "Installer publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $setupPath)) {
    throw "Installer was not created: $setupPath"
}

Get-Item -LiteralPath $setupPath
