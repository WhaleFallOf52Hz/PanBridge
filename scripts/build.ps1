param([switch]$Package, [switch]$SkipBootstrap, [string]$Sdk)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot
if (-not $SkipBootstrap) { & (Join-Path $PSScriptRoot 'bootstrap.ps1') }
if (-not $Sdk) { $Sdk = Join-Path $repository '.build-tools\dotnet\dotnet.exe' }
$env:DOTNET_CLI_HOME = Join-Path $repository '.build-tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $repository '.build-tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$project = Join-Path $repository 'src\PanBridge\PanBridge.csproj'
$output = Join-Path $repository 'dist\PanBridge'
$configuration = Join-Path $repository 'NuGet.Config'
& $Sdk publish $project -c Release --self-contained true -o $output --configfile $configuration --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'PanBridge build failed.' }
$runtime = Join-Path $output 'runtime'
$vendor = Join-Path $repository 'vendor\TboxWebdav'
New-Item -ItemType Directory -Path (Join-Path $runtime 'tbox'),(Join-Path $runtime 'rclone') -Force | Out-Null
& $Sdk publish (Join-Path $vendor 'TboxWebdav.Server.AspNetCore\TboxWebdav.Server.AspNetCore.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $runtime 'tbox') --configfile $configuration '-p:RestorePackagesWithLockFile=true' --verbosity quiet '-clp:ErrorsOnly'
if ($LASTEXITCODE -ne 0) { throw 'Tbox build failed.' }
Copy-Item -LiteralPath (Join-Path $vendor 'LICENSE.txt') -Destination (Join-Path $runtime 'tbox\LICENSE.txt') -Force

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sourceArchive = Join-Path $runtime 'tbox\source.zip'
if (Test-Path -LiteralPath $sourceArchive) { Remove-Item -LiteralPath $sourceArchive }
$archive = [System.IO.Compression.ZipFile]::Open($sourceArchive, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $vendor -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.git)[\\/]' } | ForEach-Object {
        $relative = $_.FullName.Substring($vendor.Length + 1).Replace('\','/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }

$rclone = Get-ChildItem -LiteralPath (Join-Path $repository '.build-tools\rclone') -Filter rclone.exe -Recurse | Select-Object -First 1
if (-not $rclone) { throw 'Verified rclone download is missing.' }
Copy-Item -LiteralPath $rclone.FullName -Destination (Join-Path $runtime 'rclone\rclone.exe') -Force
Get-ChildItem -LiteralPath $rclone.DirectoryName -Filter '*.txt' | Copy-Item -Destination (Join-Path $runtime 'rclone') -Force
Copy-Item -LiteralPath (Join-Path $repository '.build-tools\winfsp.msi') -Destination (Join-Path $runtime 'winfsp.msi') -Force
Copy-Item -LiteralPath (Join-Path $repository 'src\PanBridge\README.txt'),(Join-Path $repository 'src\PanBridge\THIRD-PARTY-NOTICES.txt') -Destination $output -Force
Write-Output "Built: $output\PanBridge.exe"

if ($Package) {
    [xml]$projectXml = Get-Content -LiteralPath $project -Raw -Encoding UTF8
    $version = $projectXml.Project.PropertyGroup.Version
    $packagePath = Join-Path $repository "dist\PanBridge-$version-win-x64.zip"
    if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath }
    $packageArchive = [System.IO.Compression.ZipFile]::Open($packagePath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object {
            $relative = $_.FullName.Substring($output.Length + 1)
            $relative -notmatch '^data[\\/]' -and $_.Extension -notin @('.pdb','.log','.pid') -and $_.Name -notin @('self-test.json','integration-test.json','background-test.json')
        } | ForEach-Object {
            $relative = 'PanBridge/' + $_.FullName.Substring($output.Length + 1).Replace('\','/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($packageArchive, $_.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $packageArchive.Dispose() }
    Write-Output "Package: $packagePath"
}
