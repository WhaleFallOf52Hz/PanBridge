param([switch]$Installer)
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
$goArchive = Get-VerifiedArchive $manifest.go
if (-not (Test-Path -LiteralPath (Join-Path $tools 'go-sdk\go\bin\go.exe'))) {
    Expand-Archive -LiteralPath $goArchive -DestinationPath (Join-Path $tools 'go-sdk') -Force
}
$rcloneSource = Get-VerifiedArchive $manifest.rcloneSource
if (-not (Test-Path -LiteralPath (Join-Path $tools ('rclone-source\rclone-' + $manifest.rclone.version + '\go.mod')))) {
    Expand-Archive -LiteralPath $rcloneSource -DestinationPath (Join-Path $tools 'rclone-source') -Force
}
Get-VerifiedArchive $manifest.winfsp | Out-Null
if ($Installer) {
    $compilerSetup = Get-VerifiedArchive $manifest.inno
    $signature = Get-AuthenticodeSignature -LiteralPath $compilerSetup
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Pyrsys B.V.') { throw 'Inno Setup publisher verification failed.' }
    $compilerDirectory = Join-Path $tools 'inno'
    if (-not (Test-Path -LiteralPath (Join-Path $compilerDirectory 'ISCC.exe'))) {
        $install = Start-Process -FilePath $compilerSetup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/CURRENTUSER','/NOICONS',('/DIR="' + $compilerDirectory + '"')) -WindowStyle Hidden -Wait -PassThru
        if ($install.ExitCode -ne 0) { throw 'Inno Setup compiler installation failed.' }
    }
}
Write-Output 'Verified .NET/Go SDKs, rclone source and WinFsp dependencies are ready.'
