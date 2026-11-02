param([string]$Destination)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot
$tools = Join-Path $repository '.build-tools'
$manifest = Get-Content (Join-Path $repository 'dependencies.json') -Raw | ConvertFrom-Json
$go = Join-Path $tools 'go-sdk\go\bin\go.exe'
$source = Join-Path $tools ('rclone-source\rclone-' + $manifest.rclone.version)
$entry = Join-Path $repository 'src\rclone\main.go'
if (-not $Destination) { $Destination = Join-Path $tools 'rclone-lite\rclone.exe' }
New-Item -ItemType Directory -Path (Split-Path $Destination) -Force | Out-Null
$fingerprint = $manifest.rcloneSource.hash + $manifest.go.hash + (Get-FileHash $entry -Algorithm SHA256).Hash + (Get-FileHash $PSCommandPath -Algorithm SHA256).Hash
$stamp = $Destination + '.build-key'
if ((Test-Path $Destination) -and (Test-Path $stamp) -and (Get-Content $stamp -Raw).Trim() -eq $fingerprint) { return }
if (-not (Test-Path $go) -or -not (Test-Path (Join-Path $source 'go.mod'))) { throw 'Run bootstrap.ps1 to prepare the pinned Go toolchain and rclone source.' }
Copy-Item -LiteralPath $entry -Destination (Join-Path $source 'rclone.go') -Force
$env:GOPATH = Join-Path $tools 'go-cache'
$env:GOCACHE = Join-Path $tools 'go-build-cache'
$env:GOTOOLCHAIN = 'local'
$env:CGO_ENABLED = '0'
$env:GOOS = 'windows'
$env:GOARCH = 'amd64'
Push-Location $source
try {
    & $go run -mod=readonly bin/resource_windows.go -version ('v' + $manifest.rclone.version + '-panbridge')
    if ($LASTEXITCODE -ne 0) { throw 'rclone version resource generation failed.' }
    & $go build -mod=readonly -trimpath -buildvcs=false -tags cmount -ldflags ('-s -w -X github.com/rclone/rclone/fs.Version=v' + $manifest.rclone.version + '-panbridge') -o $Destination .
    if ($LASTEXITCODE -ne 0) { throw 'Minimal rclone build failed.' }
} finally { Pop-Location }
$fingerprint | Set-Content $stamp -Encoding ASCII
