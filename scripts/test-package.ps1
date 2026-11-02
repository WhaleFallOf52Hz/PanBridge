param([Parameter(Mandatory)][string]$Application)
$ErrorActionPreference = 'Stop'
$Application = (Resolve-Path -LiteralPath $Application).Path
$root = Split-Path $Application
$checks = [Collections.Generic.List[string]]::new()
$manifestPaths = [Collections.Generic.List[string]]::new()
$payloadBytes = 0L
foreach ($line in Get-Content (Join-Path $root 'package-files.sha256')) {
    if ($line -notmatch '^([a-f0-9]{64})  (.+)$') { throw 'Invalid package manifest.' }
    $hash = $Matches[1]; $relative = $Matches[2]
    $manifestPaths.Add($relative)
    $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $path.StartsWith($root + '\',[StringComparison]::OrdinalIgnoreCase) -or $relative -match '^data/|\.dpapi$|self-test|integration-test|browser-profile') { throw 'Unsafe package entry.' }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $hash) { throw ('Package hash mismatch: ' + $relative) }
    $payloadBytes += (Get-Item -LiteralPath $path).Length
}
if ($payloadBytes -gt 220MB) { throw 'Package exceeded the 220 MiB footprint budget.' }
$checks.Add('manifest_and_size_budget')
if (@($manifestPaths | Where-Object { [IO.Path]::GetFileName($_) -eq 'coreclr.dll' }).Count -ne 1 -or
    (Test-Path (Join-Path $root 'coreclr.dll')) -or (Test-Path (Join-Path $root 'runtime\tbox\hostfxr.dll'))) { throw 'Duplicate .NET runtime found.' }
$checks.Add('single_core_runtime')
$saved = @{}
foreach ($name in @('DOTNET_ROOT','DOTNET_ROOT_X64','DOTNET_MULTILEVEL_LOOKUP')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$backend = $null
try {
    $env:DOTNET_ROOT = 'Z:\PanBridge-intentionally-missing-runtime'
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $test = Start-Process $Application -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
    if ($test.ExitCode -ne 0) { throw ('Application self-test failed: ' + (Get-Content (Join-Path $root 'self-test.json') -Raw)) }
    # Inspect the actually loaded CoreLib inside the managed process. Native
    # host trace output may replace non-ASCII path characters with question marks.
    $result = Get-Content (Join-Path $root 'self-test.json') -Raw | ConvertFrom-Json
    if (-not $result.PrivateRuntimeVerified) { throw 'Launcher did not select the private runtime.' }
    $checks.Add('private_runtime_without_system_dotnet'); $checks.Add('application_self_test')
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
    $dotnet = Join-Path $root 'runtime\dotnet\dotnet.exe'
    $bridge = Join-Path $root 'runtime\tbox\TboxWebdav.Server.AspNetCore.dll'
    $backend = Start-Process $dotnet -ArgumentList @(('"' + $bridge + '"'),'--host','127.0.0.1','--port',"$port",'--auth','UserToken','--access','ReadOnly') -WindowStyle Hidden -PassThru
    $ready = $false
    for ($attempt=0; $attempt -lt 80; $attempt++) {
        if ($backend.HasExited) { throw 'Private-runtime bridge exited during startup.' }
        try { $response=Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$port/" -TimeoutSec 2; $code=[int]$response.StatusCode }
        catch { if ($_.Exception.Response) { $code=[int]$_.Exception.Response.StatusCode } else { $code=0 } }
        if ($code -eq 401) { $ready=$true; break }
        Start-Sleep -Milliseconds 150
    }
    if (-not $ready) { throw 'Bridge authentication endpoint did not start.' }
    $checks.Add('bridge_shared_runtime_and_auth')
    $rclone = Join-Path $root 'runtime\rclone\rclone.exe'
    $backends = (& $rclone help backends | Out-String)
    if ($LASTEXITCODE -ne 0 -or $backends -notmatch 'webdav' -or $backends -match 'onedrive|azureblob|google cloud') { throw 'Unexpected rclone backend profile.' }
    & $rclone mount --help | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'rclone mount command missing.' }
    $fake = 'panbridge-package-test' | & $rclone obscure -
    if ($LASTEXITCODE -ne 0 -or -not $fake) { throw 'rclone obscure command failed.' }
    $checks.Add('rclone_mount_webdav_obscure')
    [ordered]@{Success=$true;Checks=$checks;PayloadBytes=$payloadBytes} | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $root 'package-test.json') -Encoding UTF8
    Get-Content (Join-Path $root 'package-test.json') -Raw
} finally {
    if ($backend) { if (-not $backend.HasExited) { $backend.Kill(); $backend.WaitForExit() }; $backend.Dispose() }
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
