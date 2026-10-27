<img src="src/PanBridge/Assets/PanBridge.svg" width="72" align="right" alt="PanBridge 图标">

# 交云桥 · PanBridge

上海交通大学 Pan 云盘的 Windows 挂载客户端。

将[交大云盘](https://pan.sjtu.edu.cn/)挂载为本地盘符，在资源管理器中浏览、上传和编辑文件。通过交大官方页面登录，由客户端管理凭证、盘符连接和后台运行。

本项目是第三方客户端，非学校官方应用。

![PanBridge 主界面](docs/images/overview.png)

## 功能

- **官方网页登录**：在交大登录页完成认证和验证码，无需手动复制 Cookie。
- **资源管理器访问**：将云盘挂载为盘符，支持只读、读写禁止删除、完整读写三种权限。
- **托盘后台运行**：关闭窗口后保持连接，支持启动客户端后自动挂载。
- **可配置文件缓存**：按需读取文件，可更改缓存目录和空间上限。
- **连接检测与诊断**：检查云盘访问、本地组件和上传状态，支持导出诊断报告。

## 开始使用

需要 Windows x64、可用的交大云盘账号，以及 [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)。挂载依赖 WinFsp，客户端提供安装入口；安装驱动时需要管理员权限。

1. 按[源码构建说明](#从源码构建)生成程序，运行 `dist/PanBridge/PanBridge.exe`。如果使用便携包，请先完整解压。
2. 点击“登录交大云盘”，在官方页面完成登录。
3. 首次挂载时，进入“偏好设置”安装挂载组件。
4. 在“连接与挂载”中选择盘符和权限，再点击“连接盘符”。连接成功后即可在资源管理器中使用。

**默认权限为完整读写，包括删除。** 只需浏览和下载时，请切换为只读。修改盘符或权限后，点击“保存并应用”。

关闭或最小化窗口会收起到托盘，连接继续运行。需要退出时，请使用托盘菜单中的“退出程序”；程序会等待上传结束后再断开。

## 设置与数据

在“偏好设置 → 本地存储与隐私”中，可以更改缓存目录和最大缓存空间，点击“应用缓存设置”生效。缓存默认位于 `data/cache`，清理目标上限为 1 GiB。

- 自定义缓存需使用本机 NTFS/ReFS 磁盘。切换目录时会等待上传完成并重连，旧缓存不会自动移动或删除。
- 缓存上限是清理目标，正在使用或等待上传的文件可能暂时超出上限。
- “启动客户端后自动连接”在打开程序时生效，不会设置 Windows 开机自启动。

登录凭证和设置保存在程序目录的 `data/` 中。凭证使用 Windows 当前用户的 DPAPI 加密；文件缓存不加密，可能包含已访问的云盘文件。请勿分享 `data/` 或自定义缓存目录。

学校登录状态过期后，需要重新完成官方登录。可在设置中清除本地登录数据。

更新程序前，请先从托盘退出。保留原 `data/` 目录，可在同一 Windows 用户环境中继续使用已有登录和设置。

## 从源码构建

在 Windows 上安装 Git，打开 PowerShell：

```powershell
git clone https://github.com/WhaleFallOf52Hz/PanBridge.git
cd PanBridge
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 -Package
```

构建脚本会下载并校验所需的 .NET SDK、rclone 和 WinFsp 安装包，无需预先安装 .NET SDK。首次构建需要网络连接。

输出目录为 `dist/PanBridge/`，便携包为 `dist/PanBridge-<版本>-win-x64.zip`。分发包不需要另行安装 .NET 运行时。

代码结构、依赖版本和测试方法见[开发文档](docs/development.md)。版本变更见[更新记录](CHANGELOG.md)。

## 问题反馈

请通过 [Issues](https://github.com/WhaleFallOf52Hz/PanBridge/issues) 反馈问题，并附上 Windows 版本、PanBridge 版本和复现步骤。连接故障可附“故障诊断”页面导出的报告，无需提供 Cookie 或账号密码。

## 致谢与许可

PanBridge 使用以下项目完成云盘桥接和文件挂载：

| 项目 | 用途 |
| --- | --- |
| [TboxWebdav](https://github.com/1357310795/TboxWebdav) | 交大云盘的 WebDAV 桥接 |
| [rclone](https://github.com/rclone/rclone) | 文件挂载和本地缓存 |
| [WinFsp](https://github.com/winfsp/winfsp) | Windows 文件系统支持 |

TboxWebdav 保留 GPL-2.0 许可证，本项目使用的修改版源码位于 [vendor/TboxWebdav](vendor/TboxWebdav)，并随运行包提供。组件许可见[第三方声明](src/PanBridge/THIRD-PARTY-NOTICES.txt)。

PanBridge 主程序目前尚未指定许可证；第三方组件遵循各自的许可证。
