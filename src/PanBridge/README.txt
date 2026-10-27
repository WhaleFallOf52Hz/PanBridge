交云桥（PanBridge）0.1.1 — Windows x64

完整解压后，双击 PanBridge.exe。首次启动自动弹出交大官方登录页；在页面中完成账号登录与验证码，客户端会自动获取 USER_TOKEN 并验证目录访问，无需打开开发者工具复制 Cookie。

首次挂盘：在主窗口点击“安装挂载组件”，完成 WinFsp 安装，然后选择未占用的盘符，点击“连接盘符”。可勾选“启动后自动挂载”。提供只读、读写禁止删除、完整读写三种权限；默认完整读写，可上传、修改、重命名及删除。

关闭或最小化窗口后，客户端收起到系统托盘，盘符连接继续运行。双击托盘图标恢复窗口，右键托盘图标可打开云盘、断开连接或退出程序。退出和切换权限前会等待已排队的上传；如果仍有上传失败或不能确认上传状态，会保留连接并提示重试。编辑文件时请先保存并关闭编辑器，再主动断开。

程序使用自己的随机本机端口，不影响原先的 65472 端口服务。本机 rclone 控制端口也仅监听 127.0.0.1，使用每次随机生成的访问密码，且不会写入文件。

0.1.1 修复了断开后资源管理器保留失效盘符的问题：挂载成功后发送盘符添加通知；断开时等待系统设备映射消失，再发送盘符移除通知。通知只针对本次管理的盘符，不会删除仍然连接的驱动器或修改云盘内容。

登录数据：data/session.dpapi 使用 Windows DPAPI 当前用户加密；data/browser-profile 是此应用独立的 WebView2 浏览器资料目录。只采集 pan.sjtu.edu.cn 和 jaccount.sjtu.edu.cn 的 Cookie，不记录账号密码。请勿将个人 data 文件夹一起分享。“清除登录”会移除加密凭证及应用内浏览器的 Cookie、LocalStorage 和 IndexedDB。

登录凭证受学校服务的有效期控制。过期时需重新打开登录页面；已有 jAccount 登录会话可能自动完成 SSO。客户端不会绕过验证码或多因素认证。

data/status.json 仅记录运行阶段、目录数量、盘符和错误类型，不包含凭证或文件名。rclone 使用按需读取缓存，缓存目录为 data/cache，目标上限 1 GB。读取过的文件内容可能留在缓存中。

如果从受限执行环境启动后出现网络错误，请在 Windows 资源管理器中直接双击 PanBridge.exe。客户端需要正常的 Windows HTTPS 和 WebView2 运行环境。

源码位于工作区 src/PanBridge 文件夹。复现构建：powershell -ExecutionPolicy Bypass -File scripts\build.ps1 -Package
构建依赖使用工作区 .build-tools 下的 .NET SDK、NuGet 缓存及已校验的挂载组件。分发运行包不需要安装 .NET SDK。

内置自检：PanBridge.exe --self-test。结果写入 self-test.json，只使用虚构凭证，不读取个人登录数据。

可选真实集成检查：PanBridge.exe --integration-test。此命令会使用当前保存的登录凭证，临时挂载一个空闲盘符，在云盘创建随机命名的 PanBridge-check- 目录，验证上传、修改、重命名、删除并清理自身创建的文件，结果写入 integration-test.json。
