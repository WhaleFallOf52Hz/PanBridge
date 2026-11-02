param([Parameter(Mandatory)][string]$Setup, [Parameter(Mandatory)][string]$Destination, [string]$LegacyPackage)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot
$Setup = (Resolve-Path $Setup).Path
$Destination = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $Destination) { throw 'Installer test destination must not exist.' }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
if ($LegacyPackage) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $LegacyPackage))
    try {
        foreach ($entry in $archive.Entries) {
            if (-not $entry.Name) { continue }
            $relative = $entry.FullName -replace '^PanBridge/',''
            $target = [IO.Path]::GetFullPath((Join-Path $Destination $relative))
            if (-not $target.StartsWith($Destination + '\',[StringComparison]::OrdinalIgnoreCase) -or $relative -match '^data/') { throw 'Unsafe legacy test archive.' }
            New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target)
        }
    } finally { $archive.Dispose() }
    # A changed legacy file and an unknown file must survive cleanup.
    'modified-file-must-survive' | Set-Content (Join-Path $Destination 'Microsoft.Web.WebView2.Core.xml')
}
$data = Join-Path $Destination 'data'
New-Item -ItemType Directory -Path (Join-Path $data 'cache') -Force | Out-Null
'saved-settings-sentinel' | Set-Content (Join-Path $data 'upgrade-sentinel.txt')
'pending-cache-sentinel' | Set-Content (Join-Path $data 'cache\upgrade-sentinel.txt')
'unknown-file-must-survive' | Set-Content (Join-Path $Destination 'local-notes.txt')
function Install([string]$Log) {
    $p = Start-Process $Setup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/TASKS=""',('/DIR="' + $Destination + '"'),('/LOG="' + $Log + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($p.ExitCode -ne 0) { throw ('Installation failed: ' + $p.ExitCode) }
}
$firstLog = Join-Path $Destination 'install-test.log'
Install $firstLog
if ((Test-Path (Join-Path $Destination 'coreclr.dll')) -or (Test-Path (Join-Path $Destination 'runtime\tbox\coreclr.dll'))) { throw 'Legacy duplicate runtime was not removed.' }
if ($LegacyPackage -and (Get-Content (Join-Path $Destination 'Microsoft.Web.WebView2.Core.xml') -Raw).Trim() -ne 'modified-file-must-survive') { throw 'Modified legacy file was removed.' }
& (Join-Path $PSScriptRoot 'test-package.ps1') -Application (Join-Path $Destination 'PanBridge.exe')
$runtimeFile = Get-ChildItem (Join-Path $Destination 'runtime\dotnet\shared\Microsoft.NETCore.App') -Filter coreclr.dll -Recurse | Select-Object -First 1
$marker = [datetime]::new(2001,2,3,4,5,6,[DateTimeKind]::Utc)
$runtimeFile.LastWriteTimeUtc = $marker
$secondLog = Join-Path $Destination 'reinstall-test.log'
Install $secondLog
if ((Get-Item $runtimeFile.FullName).LastWriteTimeUtc -ne $marker) { throw 'Unchanged shared runtime was needlessly overwritten.' }
if (-not (Select-String -LiteralPath $secondLog -SimpleMatch 'Reusing unchanged component:' -Quiet)) { throw 'Installer did not report component reuse.' }
foreach ($path in @('data\upgrade-sentinel.txt','data\cache\upgrade-sentinel.txt','local-notes.txt')) {
    if (-not (Test-Path (Join-Path $Destination $path))) { throw ('Upgrade removed a user file: ' + $path) }
}
[ordered]@{Success=$true;LegacyUpgrade=[bool]$LegacyPackage;UserDataPreserved=$true;ModifiedFilesPreserved=$true;SharedRuntimeReused=$true} | ConvertTo-Json | Set-Content (Join-Path $Destination 'installer-test.json')
Get-Content (Join-Path $Destination 'installer-test.json') -Raw
