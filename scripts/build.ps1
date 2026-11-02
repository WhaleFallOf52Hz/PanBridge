param([switch]$Package, [switch]$Installer, [switch]$SkipBootstrap, [string]$Sdk)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot
if ($Installer) { $Package = $true }
if (-not $SkipBootstrap) { & (Join-Path $PSScriptRoot 'bootstrap.ps1') -Installer:$Installer }
if (-not $Sdk) { $Sdk = Join-Path $repository '.build-tools\dotnet\dotnet.exe' }
$Sdk = (Resolve-Path -LiteralPath $Sdk).Path
$env:DOTNET_CLI_HOME = Join-Path $repository '.build-tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $repository '.build-tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$project = Join-Path $repository 'src\PanBridge\PanBridge.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = $projectXml.Project.PropertyGroup.Version
$tools = Join-Path $repository '.build-tools'
$stage = Join-Path $tools ('publish-' + [Guid]::NewGuid().ToString('N'))
$output = Join-Path $stage 'PanBridge'
$configuration = Join-Path $repository 'NuGet.Config'
$utf8 = [Text.UTF8Encoding]::new($false)
New-Item -ItemType Directory -Path $output -Force | Out-Null

# Publish into a clean stage, never over a user's portable installation/data.
# Both apps use a single private shared runtime, with independent processes.
& $Sdk publish $project -c Release -r win-x64 --self-contained false -o $output --configfile $configuration '-p:UseAppHost=false' '-p:NuGetAudit=false' --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'PanBridge build failed.' }
$runtime = Join-Path $output 'runtime'
$vendor = Join-Path $repository 'vendor\TboxWebdav'
$tbox = Join-Path $runtime 'tbox'
New-Item -ItemType Directory -Path $tbox,(Join-Path $runtime 'rclone') -Force | Out-Null
& $Sdk publish (Join-Path $vendor 'TboxWebdav.Server.AspNetCore\TboxWebdav.Server.AspNetCore.csproj') -c Release -r win-x64 --self-contained false -o $tbox --configfile $configuration '-p:UseAppHost=false' '-p:SatelliteResourceLanguages=zh-Hans' '-p:RestorePackagesWithLockFile=true' '-p:NuGetAudit=false' --verbosity quiet '-clp:ErrorsOnly'
if ($LASTEXITCODE -ne 0) { throw 'Tbox build failed.' }
Copy-Item -LiteralPath (Join-Path $vendor 'LICENSE.txt') -Destination $tbox
$dotnet = Join-Path $runtime 'dotnet'
$sdkRoot = Split-Path $Sdk
$frameworks = @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App','Microsoft.AspNetCore.App')
$versions = [ordered]@{}
foreach ($framework in $frameworks) {
    $source = Get-ChildItem (Join-Path $sdkRoot ('shared\' + $framework)) -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if (-not $source -or $source.Name -notmatch '^8\.0\.') { throw ('Expected a .NET 8 runtime in the pinned SDK: ' + $framework) }
    $versions[$framework] = $source.Name
    $destination = Join-Path $dotnet ('shared\' + $framework + '\' + $source.Name)
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -Path (Join-Path $source.FullName '*') -Destination $destination -Recurse -Force
}
$runtimeVersion = $versions['Microsoft.NETCore.App']
$fxr = Join-Path $dotnet ('host\fxr\' + $runtimeVersion)
New-Item -ItemType Directory -Path $fxr -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $sdkRoot ('host\fxr\' + $runtimeVersion + '\hostfxr.dll')) -Destination $fxr
foreach ($file in @('dotnet.exe','LICENSE.txt','ThirdPartyNotices.txt')) { Copy-Item -LiteralPath (Join-Path $sdkRoot $file) -Destination $dotnet }

# Resource selection only: WPF and the MVC bridge are not IL-trimmed.
$cultures = @('cs','de','es','fr','it','ja','ko','pl','pt-BR','ru','tr','zh-Hant')
Get-ChildItem -LiteralPath $output -Recurse -Directory | Where-Object { $_.Name -in $cultures } | ForEach-Object {
    $resolved = [IO.Path]::GetFullPath($_.FullName)
    if (-not $resolved.StartsWith($output + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected resource path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object { $_.Extension -in @('.pdb','.xml') } | Remove-Item -Force
& (Join-Path $PSScriptRoot 'build-launcher.ps1') -Destination (Join-Path $output 'PanBridge.exe') -RuntimeVersion $runtimeVersion -Version $version
& (Join-Path $PSScriptRoot 'build-rclone.ps1')
Copy-Item -LiteralPath (Join-Path $tools 'rclone-lite\rclone.exe') -Destination (Join-Path $runtime 'rclone')
$dependencies = Get-Content (Join-Path $repository 'dependencies.json') -Raw | ConvertFrom-Json
$rcloneSource = Join-Path $tools ('rclone-source\rclone-' + $dependencies.rclone.version)
Copy-Item -LiteralPath (Join-Path $rcloneSource 'COPYING') -Destination (Join-Path $runtime 'rclone\LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repository 'src\rclone\NOTICE.txt') -Destination (Join-Path $runtime 'rclone')
Copy-Item -LiteralPath (Join-Path $tools 'winfsp.msi') -Destination $runtime
Copy-Item -LiteralPath (Join-Path $repository 'src\PanBridge\README.txt'),(Join-Path $repository 'src\PanBridge\THIRD-PARTY-NOTICES.txt') -Destination $output

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open((Join-Path $tbox 'source.zip'), [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $vendor -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.git)[\\/]' } | ForEach-Object {
        $relative = $_.FullName.Substring($vendor.Length + 1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
$size = [ordered]@{}
$size['Application'] = (Get-ChildItem $output -File | Measure-Object Length -Sum).Sum
foreach ($name in @('dotnet','tbox','rclone')) { $size[$name] = (Get-ChildItem (Join-Path $runtime $name) -Recurse -File | Measure-Object Length -Sum).Sum }
$size['WinFspInstaller'] = (Get-Item (Join-Path $runtime 'winfsp.msi')).Length
$size['TotalBytes'] = (Get-ChildItem $output -Recurse -File | Measure-Object Length -Sum).Sum
[IO.File]::WriteAllText((Join-Path $output 'package-info.json'), (([ordered]@{Version=$version;Frameworks=$versions;Size=$size;RcloneVersion=($dependencies.rclone.version + '-panbridge');RcloneProfile='local,webdav,cmount,obscure,version (crypt is a transitive dependency)'}) | ConvertTo-Json -Depth 5), $utf8)
$entries = @(Get-ChildItem $output -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($output.Length + 1).Replace('\','/')
    [pscustomobject]@{Relative=$relative;Hash=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
$manifest = ($entries | ForEach-Object { $_.Hash + '  ' + $_.Relative }) -join "`n"
[IO.File]::WriteAllText((Join-Path $output 'package-files.sha256'), $manifest + "`n", $utf8)
# Check individual file hashes to preserve unchanged installed components.
$includes = @($entries | ForEach-Object {
    $relative = $_.Relative.Replace('/','\')
    $parent = Split-Path $relative
    $dest = if ($parent) { '{app}\' + $parent } else { '{app}' }
    'Source: "{#PayloadDir}\' + $relative + '"; DestDir: "' + $dest + '"; Flags: ignoreversion; Check: ShouldInstallFile(''' + $_.Relative + ''', ''' + $_.Hash + ''')'
})
$includes += 'Source: "{#PayloadDir}\package-files.sha256"; DestDir: "{app}"; Flags: ignoreversion'
$fileInclude = Join-Path $stage 'payload-files.iss'
[IO.File]::WriteAllText($fileInclude, ($includes -join "`n"), $utf8)
$dist = Join-Path $repository 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
if ($Package) {
    $packagePath = Join-Path $dist "PanBridge-$version-win-x64.zip"
    if (Test-Path $packagePath) { Remove-Item -LiteralPath $packagePath }
    $zip = [IO.Compression.ZipFile]::Open($packagePath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem $output -Recurse -File | ForEach-Object {
            $relative = 'PanBridge/' + $_.FullName.Substring($output.Length + 1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $zip.Dispose() }
    Write-Output "Package: $packagePath"
}
if ($Installer) {
    $compiler = Join-Path $tools 'inno\ISCC.exe'
    & $compiler '/Q' ('/DAppVersion=' + $version) ('/DPayloadDir=' + $output) ('/DPayloadFiles=' + $fileInclude) ('/DRepositoryDir=' + $repository) ('/DOutputDir=' + $dist) (Join-Path $repository 'installer\PanBridge.iss')
    if ($LASTEXITCODE -ne 0) { throw 'EXE installer compilation failed.' }
    Write-Output "Installer: $dist\PanBridge-$version-Setup-x64.exe"
}
# A rebuild copies only package files; local data in this versioned output survives.
$runnable = Join-Path $dist "PanBridge-$version"
New-Item -ItemType Directory -Path $runnable -Force | Out-Null
Copy-Item -Path (Join-Path $output '*') -Destination $runnable -Recurse -Force
$size | ConvertTo-Json | Write-Output
Write-Output "Built: $runnable\PanBridge.exe"
$resolvedStage = [IO.Path]::GetFullPath($stage)
if ([IO.Path]::GetDirectoryName($resolvedStage) -ne [IO.Path]::GetFullPath($tools) -or -not [IO.Path]::GetFileName($resolvedStage).StartsWith('publish-')) { throw 'Unexpected staging directory.' }
Remove-Item -LiteralPath $resolvedStage -Recurse -Force
