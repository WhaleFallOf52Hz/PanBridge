# 开发与测试

PanBridge 使用 C#、.NET 8 和 WPF，官方登录窗口基于 WebView2。一个原生 C 启动入口通过 hostfxr 在当前进程启动 WPF。主程序与独立 Tbox 子进程复用 `runtime/dotnet` 内的 Core/Desktop/ASP.NET Core 共享框架；精简 rclone 作为独立子进程运行，WinFsp 提供 Windows 文件系统支持。

## 构建

先安装 MinGW-w64，确保 `gcc` 和 `windres` 位于 PATH。在 Windows PowerShell 中，从仓库根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 -Installer
```

脚本通过 `scripts/bootstrap.ps1` 准备并校验 .NET/Go SDK、固定 rclone 源码和其他依赖，再构建客户端、TboxWebdav、原生入口和精简 rclone。所需下载版本及校验值固定在 [dependencies.json](../dependencies.json)，.NET SDK 版本由 [global.json](../global.json) 指定，NuGet 与 Go 使用各自的锁文件校验依赖。构建工具均放在 `.build-tools/` 中，不安装到系统。

`-Installer` 同时生成便携包与 EXE 安装包，并准备已固定校验值的 Inno Setup 编译器；`-Package` 仅生成便携包。安装脚本位于 `installer/PanBridge.iss`，ZIP 和安装器均从同一全新暂存目录生成，不遍历已有运行目录或个人数据。发布不做 IL trimming；语言资源只保留简体中文和英文回退。

- `.build-tools/`：SDK、依赖下载及构建缓存。
- `dist/PanBridge-<版本>/`：可直接运行的程序及组件；重新构建不会删除已有 `data/`。
- `dist/PanBridge-<版本>-win-x64.zip`：便携包，排除登录数据、文件缓存和测试结果。
- `dist/PanBridge-<版本>-Setup-x64.exe`：当前用户安装包。

若要覆盖正在运行的程序，请先通过托盘菜单退出，等待上传结束。构建产物和个人数据不纳入版本管理。

## 代码结构

| 路径 | 内容 |
| --- | --- |
| `src/PanBridge/` | WPF 客户端、登录、凭证存储和挂载管理 |
| `src/PanBridge/Assets/` | SVG 图标及导出的 PNG、ICO |
| `src/PanBridge.Launcher/` | 原生 Windows 入口、图标版本资源与私有运行时定位 |
| `src/rclone/` | 精简 rclone 的入口和构建说明 |
| `vendor/TboxWebdav/` | 固定版本的 TboxWebdav 源码和本地补丁 |
| `scripts/` | 依赖准备、构建、图标导出及界面测试 |

`MainWindow` 处理窗口生命周期、托盘和原生对话框，`WorkspaceViewModel` 管理界面状态和命令。`TboxBackend`、`MountManager`、`SessionVault` 分别管理桥接服务、盘符与上传队列、加密凭证；缓存设置由 `CacheStorage` 校验，诊断检查由 `Diagnostics` 执行。共享样式位于 `App.xaml`。

图标的矢量源和导出方法见[图标设计说明](../src/PanBridge/Assets/design-notes.md)。TboxWebdav 的上游版本与补丁说明见 [UPSTREAM.json](../vendor/TboxWebdav/UPSTREAM.json)。

## 测试

### 本地自检

```powershell
dist\PanBridge-0.4.0\PanBridge.exe --self-test
```

使用虚构凭证和隔离的缓存目录，检查 Cookie 规则、DPAPI 加密、WebDAV 解析、设置兼容性、命令可用性和缓存保护逻辑。不读取个人登录凭证，也不访问云盘。结果写入程序目录的 `self-test.json`。

启动项测试仅写入 `HKCU\Software\PanBridge\Tests\<随机标识>` 并在完成后清理，不修改实际启动项。实际功能使用 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，只读 Windows 的 `StartupApproved` 状态，不重置用户在系统中的禁用选择。

`PanBridge.exe --startup` 用于已启用的 Windows 启动项；`--background` 可独立检查冷启动托盘行为；`--quit` 请求运行中的实例等待上传完成并安全退出。

## Release

推送与项目版本一致的 `v<版本>` 标签后，`.github/workflows/release.yml` 在 Windows Runner 构建 EXE 与 ZIP，执行安装后自检，再发布到 GitHub Release，并附上 SHA-256 校验文件。发布使用仓库范围的 `GITHUB_TOKEN`，不需要在源码中保存个人令牌。

`package-info.json` 记录组件版本与分项体积，`package-files.sha256` 记录分发文件哈希。安装器复用哈希一致的文件，在安装成功后移除旧清单中不再分发且哈希仍匹配的文件；路径检查保护个人数据和目录联接。首次升级使用 `installer/legacy-v0.3.1.sha256`，保留未知或被修改的文件。

```powershell
scripts\test-package.ps1 -Application dist\PanBridge-0.4.0\PanBridge.exe
scripts\test-installer.ps1 -Setup dist\PanBridge-0.4.0-Setup-x64.exe -Destination "$env:TEMP\PanBridge-install-test"
```

分发检查验证单套 Core 运行时、无系统 .NET 时的启动、桥接认证、精简 rclone 命令、文件完整性和 220 MiB 体积上限。安装检查会注册测试安装目录，并验证重复安装时组件文件不被改写；可额外传入 `-LegacyPackage <旧版ZIP>` 验证旧版升级。请在隔离环境执行安装检查。

### 界面测试

先在 Windows 桌面启动构建后的客户端，再执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\test-ui.ps1 -RunDiagnostics
```

检查页面导航、关键控件、较小窗口下的排布和诊断结果展示。`-RunDiagnostics` 会检测网络与云盘连接，不写入云盘文件。结果写入 `.build-tools/ui-smoke-test.json`。

### 云盘读写测试

需要已保存的有效登录：

```powershell
dist\PanBridge-0.4.0\PanBridge.exe --integration-test
```

此测试会临时挂载一个空闲盘符，在云盘创建随机命名的专用目录，验证上传、覆盖修改、文件重命名、文件夹重命名、删除和正常断开，最后清理自身创建的文件。它会实际写入云盘，请在允许进行读写测试的账号中运行。不会修改已有文件，结果写入程序目录的 `integration-test.json`。

### 盘符生命周期测试

先打开资源管理器的“此电脑”窗口：

```powershell
dist\PanBridge-0.4.0\PanBridge.exe --drive-lifecycle-test
```

使用独立的临时只读盘符和卷名，检查挂载后的图标显示，以及断开后的设备映射和图标移除。不写入云盘文件，也不主动断开主客户端的盘符。结果写入程序目录的 `drive-lifecycle-test.json`。

## 数据与诊断

程序的登录资料、设置和默认文件缓存位于运行目录的 `data/`。凭证由 Windows DPAPI 按当前用户加密，不能将这类文件作为测试夹具或随运行包分发。

`data/status.json` 记录运行阶段、目录数量、盘符和错误类型，不记录凭证和云盘文件名。诊断报告同样不导出 Cookie、令牌、账号和文件名。反馈问题时优先使用客户端导出的报告。

切换缓存目录前需确认上传完成。缓存元数据缺失、损坏或标记为未上传时，保留原缓存并阻止切换；不要通过删除缓存来绕过此检查。缓存格式依赖固定的 rclone 版本，升级组件时应同时检查这部分兼容性。
