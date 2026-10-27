# 交云桥 · PanBridge

将上海交通大学 Pan 云盘连接为 Windows 资源管理器中的磁盘。一个客户端完成官方网页登录、Cookie 加密保存、后台连接和盘符挂载。

当前版本为 Windows x64 桌面初版，使用 WPF、WebView2、TboxWebdav、rclone 和 WinFsp。

## 使用

完整解压运行包，双击 `PanBridge.exe`。首次在弹出的交大官方页面完成登录和验证码，客户端自动获取云盘 Cookie，不需要复制令牌。

安装挂载组件后，选择盘符并连接。支持只读、读写禁止删除、完整读写三种模式。关闭或最小化窗口后仍在系统托盘运行；双击托盘图标或再次启动程序恢复主窗口，右键托盘可退出。

登录数据位于运行目录的 `data/`，使用 Windows 当前用户的 DPAPI 加密。学校登录凭证过期后需要重新完成官方登录。此项目是第三方客户端。

## 仓库结构

```text
src/PanBridge/       WPF 客户端、认证、凭证存储与进程管理
vendor/TboxWebdav/   固定版本的上游源码及文件版本缓存修复
scripts/            依赖准备、构建与便携包生成
dependencies.json   固定版本、官方下载地址及校验值
global.json         .NET SDK 版本
```

`dist/`、`.build-tools/`、个人 `data/`、Cookie、缓存和旧实验目录均不纳入版本管理。原始 `JboxWebdav/`、`TboxWebdav/`、`tbox-local/` 只保留作本地历史参考；正式构建只使用 `src/` 和 `vendor/`。

## 构建

在正常 Windows 用户环境的 PowerShell 中，从仓库根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 -Package
```

首次会下载并校验固定版本的 .NET SDK、rclone 和 WinFsp 安装包，工具缓存保存在 `.build-tools/`；不要求全局安装 SDK。NuGet 包通过仓库的 `NuGet.Config` 从官方源恢复，项目锁文件纳入版本管理。

产物：

- `dist/PanBridge/PanBridge.exe`
- `dist/PanBridge-0.1.2-win-x64.zip`

便携包自动排除个人登录数据、缓存及测试结果。更新正在运行的程序前，请从托盘菜单退出，以便等待文件上传完成。

## 验证

```powershell
dist\PanBridge\PanBridge.exe --self-test
```

自检使用虚构凭证，验证 Cookie 识别、有效期与域名限制、DPAPI 加密存储和 WebDAV 响应解析，结果写入程序目录的 `self-test.json`。

```powershell
dist\PanBridge\PanBridge.exe --integration-test
```

真实集成检查需要已保存的有效登录。它会在云盘新建随机命名的专用测试目录，验证挂盘、上传、不同大小与相同大小的覆盖修改、重命名、删除和正常断开，然后清理自身创建的测试文件。不会修改已有文件，结果写入 `integration-test.json`。

打开资源管理器“此电脑”窗口后，可运行只读盘符生命周期回归检查：

```powershell
dist\PanBridge\PanBridge.exe --drive-lifecycle-test
```

此检查使用独立的临时只读盘符和卷名，不写入云盘文件，也不会断开主客户端的盘符。它确认临时盘符出现在已有资源管理器窗口中，再验证断开后系统设备映射和窗口中的图标都已消失。结果写入 `drive-lifecycle-test.json`。

## 上游组件

TboxWebdav 的版本、来源和本地补丁记录在 `vendor/TboxWebdav/UPSTREAM.json`。它作为独立进程运行，保留 GPL-2.0 许可证，修改后的对应源码随运行包提供。其他组件见 `src/PanBridge/THIRD-PARTY-NOTICES.txt`。

## 图标设计源

应用图标使用手工绘制的 [SVG](src/PanBridge/Assets/PanBridge.svg)，PNG 与多尺寸 ICO 均由它导出。曲线、配色与导出方式见[设计说明](src/PanBridge/Assets/design-notes.md)。
