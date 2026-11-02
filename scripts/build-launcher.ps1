param([Parameter(Mandatory)][string]$Destination, [Parameter(Mandatory)][string]$RuntimeVersion, [Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot
$stage = Join-Path $repository '.build-tools\launcher'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
if (-not (Get-Command gcc -ErrorAction SilentlyContinue) -or -not (Get-Command windres -ErrorAction SilentlyContinue)) { throw 'MinGW-w64 gcc and windres are required to build the native Windows launcher.' }
$utf8 = [System.Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $stage 'runtime.h'), ('#define PANBRIDGE_RUNTIME_VERSION L"' + $RuntimeVersion + '"'), $utf8)
$numeric = ($Version.Split('.') + @('0'))[0..3] -join ','
$icon = (Join-Path $repository 'src\PanBridge\Assets\PanBridge.ico').Replace('\','/')
$appManifest = (Join-Path $repository 'src\PanBridge.Launcher\app.manifest').Replace('\','/')
$resource = @"
#include <windows.h>
1 ICON "$icon"
1 RT_MANIFEST "$appManifest"
1 VERSIONINFO
FILEVERSION $numeric
PRODUCTVERSION $numeric
FILETYPE VFT_APP
BEGIN
  BLOCK "StringFileInfo"
  BEGIN
    BLOCK "040904b0"
    BEGIN
      VALUE "CompanyName", "PanBridge\0"
      VALUE "FileDescription", "PanBridge\0"
      VALUE "FileVersion", "$Version\0"
      VALUE "ProductName", "PanBridge\0"
      VALUE "ProductVersion", "$Version\0"
      VALUE "OriginalFilename", "PanBridge.exe\0"
    END
  END
  BLOCK "VarFileInfo"
  BEGIN
    VALUE "Translation", 0x409, 1200
  END
END
"@
$rc = Join-Path $stage 'launcher.rc'
$object = Join-Path $stage 'launcher.o'
[IO.File]::WriteAllText($rc, $resource, $utf8)
& windres -i $rc -o $object
if ($LASTEXITCODE -ne 0) { throw 'Launcher resource compilation failed.' }
& gcc -municode -mwindows -Os -s -static-libgcc -Wall -Wextra -Werror -I $stage (Join-Path $repository 'src\PanBridge.Launcher\main.c') $object -lshell32 -o $Destination
if ($LASTEXITCODE -ne 0) { throw 'Native launcher compilation failed.' }
