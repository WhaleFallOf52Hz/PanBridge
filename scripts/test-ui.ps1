param([string]$Application, [switch]$RunDiagnostics)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot
if (-not $Application) { $Application = Join-Path $repository 'dist\PanBridge\PanBridge.exe' }
$Application = (Resolve-Path -LiteralPath $Application).Path
Add-Type -AssemblyName UIAutomationClient
$client = Get-Process -Name PanBridge -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $Application } | Select-Object -First 1
if (-not $client) { throw 'Start PanBridge in the normal Windows desktop session first.' }
$activate = [System.Threading.EventWaitHandle]::OpenExisting('Local\PanBridge.ShowWindow')
try { [void]$activate.Set() } finally { $activate.Dispose() }
Start-Sleep -Milliseconds 400
$client.Refresh()
$window = [System.Windows.Automation.AutomationElement]::FromHandle($client.MainWindowHandle)
$checks = [System.Collections.Generic.List[string]]::new()
function Find-Control([string]$Id) {
    $element = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id))
    if (-not $element) { throw "Control unavailable: $Id" }
    return $element
}
function Select-Page([string]$Id) {
    (Find-Control $Id).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 150
}
function Assert-Visible([string]$Id) {
    if ((Find-Control $Id).Current.IsOffscreen) { throw "Control is clipped or offscreen: $Id" }
    $checks.Add("visible:$Id")
}
try {
    Select-Page 'OverviewTab'
    Assert-Visible 'PrimaryButton'
    Select-Page 'ConnectionTab'
    foreach ($id in @('LoginButton','DrivePicker','AccessPicker','ApplyButton','MountButton','DisconnectButton','TestButton')) { Assert-Visible $id }
    Select-Page 'DiagnosticsTab'
    Assert-Visible 'DiagnoseButton'
    Assert-Visible 'ExportButton'
    if ($RunDiagnostics) {
        (Find-Control 'DiagnoseButton').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 200
        for ($attempt = 0; $attempt -lt 120; $attempt++) {
            if ((Find-Control 'DiagnoseButton').Current.IsEnabled -and (Find-Control 'ExportButton').Current.IsEnabled) { break }
            Start-Sleep -Milliseconds 500
        }
        if (-not (Find-Control 'ExportButton').Current.IsEnabled) { throw 'Diagnostics did not complete within 60 seconds.' }
        $expected = @('挂载组件 · WinFsp','云盘桥接 · Tbox','文件挂载 · rclone','官方登录 · WebView2','本地数据与缓存','云盘网络 · HTTPS','云盘登录与根目录','本地桥接服务','盘符与上传队列')
        foreach ($name in $expected) {
            $row = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $name))
            if (-not $row) { throw "Diagnostic result missing: $name" }
        }
        $checks.Add('diagnostics:nine_results_and_report_available')
    }
    Select-Page 'SettingsTab'
    foreach ($id in @('AutoMountCheck','OpenAfterMountCheck','TrayHintCheck')) { Assert-Visible $id }
    Select-Page 'AboutTab'
    $checks.Add('about:navigation_available')
    # Check the minimum supported desktop size, then restore the user's window size.
    $bounds = $window.Current.BoundingRectangle
    $transform = $window.GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern)
    if ($transform.Current.CanResize) {
        try {
            $transform.Resize(1160,840)
            Select-Page 'ConnectionTab'
            foreach ($id in @('LoginButton','DrivePicker','AccessPicker','ApplyButton')) { Assert-Visible $id }
            $checks.Add('layout:small_window')
        } finally { $transform.Resize($bounds.Width,$bounds.Height) }
    }
    $report = [ordered]@{ Success=$true; Checks=$checks.ToArray(); Time=(Get-Date).ToString('o') }
    $output = Join-Path $repository '.build-tools\ui-smoke-test.json'
    New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null
    $report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $output -Encoding UTF8
    $report | ConvertTo-Json -Depth 4
} finally { Select-Page 'OverviewTab' }
