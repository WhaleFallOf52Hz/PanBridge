param()
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot
$tools = Join-Path $repository '.build-tools'
New-Item -ItemType Directory -Path $tools -Force | Out-Null
$manifest = Get-Content -LiteralPath (Join-Path $repository 'dependencies.json') -Raw | ConvertFrom-Json

function Get-VerifiedArchive($dependency) {
    $path = Join-Path $tools $dependency.archive
    $valid = (Test-Path -LiteralPath $path) -and ((Get-FileHash -LiteralPath $path -Algorithm $dependency.algorithm).Hash -eq $dependency.hash)
    if (-not $valid) {
        Write-Host "Downloading $($dependency.archive)..."
        $partial = $path + '.partial'
        Invoke-WebRequest -Uri $dependency.url -OutFile $partial -UseBasicParsing -UserAgent 'PanBridge-Build' -TimeoutSec 180
        if ((Get-FileHash -LiteralPath $partial -Algorithm $dependency.algorithm).Hash -ne $dependency.hash) {
            Remove-Item -LiteralPath $partial
            throw "Dependency checksum mismatch: $($dependency.archive)"
        }
        Move-Item -LiteralPath $partial -Destination $path -Force
    }
    return $path
}

$sdkArchive = Get-VerifiedArchive $manifest.dotnet
if (-not (Test-Path -LiteralPath (Join-Path $tools 'dotnet\dotnet.exe'))) {
    Expand-Archive -LiteralPath $sdkArchive -DestinationPath (Join-Path $tools 'dotnet') -Force
}
$rcloneArchive = Get-VerifiedArchive $manifest.rclone
if (-not (Get-ChildItem -LiteralPath (Join-Path $tools 'rclone') -Filter rclone.exe -Recurse -ErrorAction SilentlyContinue)) {
    Expand-Archive -LiteralPath $rcloneArchive -DestinationPath (Join-Path $tools 'rclone') -Force
}
Get-VerifiedArchive $manifest.winfsp | Out-Null
Write-Output 'Verified SDK, rclone and WinFsp dependencies are ready.'
