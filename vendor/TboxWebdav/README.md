![](https://s2.loli.net/2025/02/25/4QbqRWm9yk7CU2L.png)

<p align="center">
  <img align="center" src="https://img.shields.io/github/license/1357310795/TboxWebdav" /> 
  <img align="center" src="https://img.shields.io/github/forks/1357310795/TboxWebdav" /> 
  <img align="center" src="https://img.shields.io/github/stars/1357310795/TboxWebdav" /> 
  <img align="center" src="https://img.shields.io/github/v/release/1357310795/TboxWebdav?include_prereleases" /> 
  <img align="center" src="https://img.shields.io/github/downloads/1357310795/TboxWebdav/total" />
  <!-- <img align="center" src="https://img.shields.io/github/deployments/1357310795/TboxWebdav/github-pages?label=Docs%20Build" />  -->
</p>

## 程序简介
程序对接了新交大云盘（腾讯 SMH）API 和 WebDAV 协议，用户可以通过 WebDAV 协议访问新交大云盘，借助 Raidrive 可将新交大云盘挂载为网络磁盘，与资源管理器深度整合，使用体验接近本地磁盘。

## 下载
本程序为便携版程序，仅提供命令行界面。请在 [Github Releases](https://github.com/1357310795/TboxWebdav/releases) 下载后直接运行。

- 若选择“with-runtime”类型的二进制程序包，则下载后可以直接运行
- 若选择“no-runtime”类型的二进制程序包，则下载后还需要安装 [ASP.NET Core Runtime 8.0](https://dotnet.microsoft.com/zh-cn/download/dotnet/thank-you/runtime-aspnetcore-8.0.13-windows-x64-installer) 和 [.NET Runtime 8.0](https://dotnet.microsoft.com/zh-cn/download/dotnet/thank-you/runtime-8.0.13-windows-x64-installer) 才可以运行

## 使用
- 【方式一】使用默认参数，直接运行程序（推荐）
连接时随便输入一个用户名，密码可以用 JAAuthCookie 或者 UserToken

![](https://s2.loli.net/2025/02/25/g5MPKsctuWqdox7.png)

- 【方式二】允许匿名登录（仅限自己使用！注意数据安全！）
```
TboxWebdav.Server.AspNetCore --auth None -C {你的 JAAuthCookie}
```
或者
```
TboxWebdav.Server.AspNetCore --auth None -T {你的 UserToken}
```

- 【方式三】自定义用户名密码
如果觉得密码太长不好看，可以用自定义的方式，这时推荐使用配置文件，详见下一节
```
TboxWebdav.Server.AspNetCore -c config.yaml
```

### 去哪里找认证凭证？
- 对于 JAAuthCookie：
  - 请先随便找一个需要 jAccount 认证的网站（比如 my.sjtu.edu.cn），登录进去
  - 然后打开 https://jaccount.sjtu.edu.cn/jaccount/
  - 按下 F12 打开开发者工具，在“应用程序——存储——Cookie”里面可以看到 JAAuthCookie

  ![](https://s2.loli.net/2025/02/25/jZwpTbMv7yBDzUC.png)

- 对于 UserToken：
  - 登录新云盘 https://pan.sjtu.edu.cn/
  - 按下 F12 打开开发者工具，在“应用程序——存储——Cookie”里面可以看到 UserToken：

  ![](https://s2.loli.net/2025/02/25/HvkTw4xS5OhfYgI.png)

## 参考
命令行参数：
```
Usage:
  TboxWebdav.Server.AspNetCore [options]

Options:
  -c, --config <config>                          指定一个 YAML 格式的配置文件。使用配置文件时，其他命令行参数全部无效。
  -p, --port <port>                              指定 HTTP 服务监听的端口号。 [default: 65472]
  -h, --host <host>                              指定 HTTP 服务监听的主机名或 IP 地址。 [default: localhost]
  --cachesize <cachesize>                        指定缓存空间的大小（不建议小于 10MB）。 [default: 20971520]
  --auth <Custom|JaCookie|Mixed|None|UserToken>  指定 WebDav 服务的认证方式。支持的值包括 'None'、'JaCookie'、'UserToken'、'Custom'、'Mixed'。
                                                  - None 表示 WebDav 服务使用匿名认证，此时必须指定 --cookie 或者 --token 作为单用户空间的云盘认证凭证。
                                                  - JaCookie 表示 WebDav 服务使用 jAccount 的 JAAuthCookie 进行认证
                                                  - UserToken 表示 WebDav 服务使用 新云盘 的 UserToken 进行认证
                                                  - Custom 表示 WebDav 服务使用自定义用户名密码进行认证，此时必须指定 --cookie  或者 --token 作为单用户空间的云盘认证凭证，或者使用配置文件进行更复杂的认证策略。
                                                  - Mixed 表示 WebDav 服务使用混合认证，同时支持 JaCookie 和 UserToken 两种认证方式，并在满足条件的情况下支持 Custom 认证方式。 [default: Mixed]
  -U, --username <username>                      指定用于 WebDav 服务认证的自定义用户名。
  -P, --password <password>                      指定用于 WebDav 服务认证的自定义密码。
  -C, --cookie <cookie>                          指定用于 jAccount 认证的 JAAuthCookie 字符串。
  -T, --token <token>                            指定用于 新云盘 认证的用户令牌。
  --access <Full|NoDelete|ReadOnly>              指定对于 新云盘 的访问权限。 [default: Full]
  --version                                      Show version information
  -?, -h, --help                                 Show help and usage information
```

配置文件参考：
```yaml
Host: 0.0.0.0 # HTTP 服务监听主机，默认 localhost
Port: 5047 # HTTP 服务监听端口，默认 65472
CacheSize: 20971520 # 下载缓存大小，默认 20MB
AuthMode: Mixed # 授权模式，默认 Mixed
Cookie: 123abc # jAccount 认证凭据（JAAuthCookie），默认为空
UserToken: 123abc # 新云盘认证凭据，默认为空
AccessMode: Full # 访问模式，默认为 Full
Users: # 设置自定义授权模式的用户名和密码
  - UserName: admin
    PassWord: admin
    UserToken: 123abc
  - UserName: test
    PassWord: test
    Cookie: 123abc
    AccessMode: ReadOnly
```

## 说在最后
如果觉得程序好用的话，请点亮右上角的 Star 哦~

以及，欢迎Bug Report & Pull Request