using Microsoft.AspNetCore.Server.Kestrel.Core;
using TboxWebdav.Server.Handlers;
using TboxWebdav.Server.Modules;
using TboxWebdav.Server.Modules.Tbox;
using TboxWebdav.Server.Modules.Tbox.Services;
using TboxWebdav.Server.Modules.Webdav;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;
using System.CommandLine;
using TboxWebdav.Server.AspNetCore.Models;
using TboxWebdav.Server.AspNetCore.Middlewares;

namespace TboxWebdav.Server.AspNetCore
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var configFileOption = new Option<FileInfo?>(
                aliases: new string[] { "--config", "-c" },
                description: "指定一个 YAML 格式的配置文件。使用配置文件时，其他命令行参数全部无效。"
            );
            var portOption = new Option<int>(
                aliases: new string[] { "--port", "-p" },
                getDefaultValue: () => 65472,
                description: "指定 HTTP 服务监听的端口号。"
            );
            var hostOption = new Option<string>(
                aliases: new string[] { "--host", "-h" },
                getDefaultValue: () => "localhost",
                description: "指定 HTTP 服务监听的主机名或 IP 地址。"
            );
            var cacheSizeOption = new Option<int>(
                aliases: new string[] { "--cachesize" },
                getDefaultValue: () => 20 * 1024 * 1024,
                description: "指定缓存空间的大小（不建议小于 10MB）。"
            );
            var authModeOption = new Option<AppAuthMode>(
                aliases: new string[] { "--auth" },
                getDefaultValue: () => AppAuthMode.Mixed,
                description: """
                指定 WebDav 服务的认证方式。支持的值包括 'None'、'JaCookie'、'UserToken'、'Custom'、'Mixed'。
                 - None 表示 WebDav 服务使用匿名认证，此时必须指定 --cookie 或者 --token 作为单用户空间的云盘认证凭证。
                 - JaCookie 表示 WebDav 服务使用 jAccount 的 JAAuthCookie 进行认证
                 - UserToken 表示 WebDav 服务使用 新云盘 的 UserToken 进行认证
                 - Custom 表示 WebDav 服务使用自定义用户名密码进行认证，此时必须指定 --cookie 或者 --token 作为单用户空间的云盘认证凭证，或者使用配置文件进行更复杂的认证策略。
                 - Mixed 表示 WebDav 服务使用混合认证，同时支持 JaCookie 和 UserToken 两种认证方式，并在满足条件的情况下支持 Custom 认证方式。
                """
            );
            var userNameOption = new Option<string?>(
                aliases: new string[] { "--username", "-U" },
                description: "指定用于 WebDav 服务认证的自定义用户名。"
            );
            var passwordOption = new Option<string?>(
                aliases: new string[] { "--password", "-P" },
                description: "指定用于 WebDav 服务认证的自定义密码。"
            );
            var cookieOption = new Option<string?>(
                aliases: new string[] { "--cookie", "-C" },
                description: "指定用于 jAccount 认证的 JAAuthCookie 字符串。"
            );
            var userTokenOption = new Option<string?>(
                aliases: new string[] { "--token", "-T" },
                description: "指定用于 新云盘 认证的用户令牌。"
            );
            var accessModeOption = new Option<AppAccessMode>(
                aliases: new string[] { "--access" },
                getDefaultValue: () => AppAccessMode.Full,
                description: "指定对于 新云盘 的访问权限。"
            );     

            var rootCommand = new RootCommand("Welcome to TboxWebdav!");
            rootCommand.AddOption(configFileOption);
            rootCommand.AddOption(portOption);
            rootCommand.AddOption(hostOption);
            rootCommand.AddOption(cacheSizeOption);
            rootCommand.AddOption(authModeOption);
            rootCommand.AddOption(userNameOption);
            rootCommand.AddOption(passwordOption);
            rootCommand.AddOption(cookieOption);
            rootCommand.AddOption(userTokenOption);
            rootCommand.AddOption(accessModeOption);

            rootCommand.SetHandler((appOptions) =>
            {
                RunApp(appOptions);
            }, new AppCmdOptionBinder(
                configFileOption,
                portOption,
                hostOption,
                cacheSizeOption,
                authModeOption,
                userNameOption,
                passwordOption,
                cookieOption,
                userTokenOption,
                accessModeOption
            ));

            await rootCommand.InvokeAsync(args);
        }
        public static void RunApp(AppCmdOption appOptions)
        {
            if (appOptions.IsError)
            {
                Console.Error.WriteLine(appOptions.Message);
                return;
            }
            AppCmdOption.Default = appOptions;

            var builder = WebApplication.CreateBuilder();

            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 20L * 1024 * 1024 * 1024;
            });

            builder.Services.AddControllers();
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddTransient<IStoreCollection, TboxStoreCollection>();
            builder.Services.AddTransient<IStoreItem, TboxStoreItem>();
            builder.Services.AddTransient<TboxUploader>();
            builder.Services.AddScoped<IStore, TboxStore>();
            builder.Services.AddScoped<IWebDavContext, WebDavContext>();
            builder.Services.AddScoped<IWebDavDispatcher, WebDavDispatcher>();
            builder.Services.AddScoped<IWebDavStoreContext, WebDavStoreContext>();
            builder.Services.AddScoped<JaCookieProvider>();
            builder.Services.AddScoped<TboxUserTokenProvider>();
            builder.Services.AddScoped<TboxService>();
            builder.Services.AddScoped<TboxSpaceInfoProvider>();
            builder.Services.AddScoped<TboxSpaceCredProvider>();
            builder.Services.AddScoped<TboxParameterResolverProvider>();
            builder.Services.AddSingleton<HttpClientFactory>();

            builder.Services.AddKeyedScoped<IWebDavHandler, GetHandler>(WebDavRequestMethods.GET.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, PropFindHandler>(WebDavRequestMethods.PROPFIND.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, HeadHandler>(WebDavRequestMethods.HEAD.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, OptionsHandler>(WebDavRequestMethods.OPTIONS.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, DeleteHandler>(WebDavRequestMethods.DELETE.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, PutHandler>(WebDavRequestMethods.PUT.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, LockHandler>(WebDavRequestMethods.LOCK.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, UnlockHandler>(WebDavRequestMethods.UNLOCK.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, CopyHandler>(WebDavRequestMethods.COPY.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, MkcolHandler>(WebDavRequestMethods.MKCOL.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, MoveHandler>(WebDavRequestMethods.MOVE.ToString());
            builder.Services.AddKeyedScoped<IWebDavHandler, PropPatchHandler>(WebDavRequestMethods.PROPPATCH.ToString());

            builder.Services.AddMemoryCache(); // Singleton

            builder.WebHost.UseUrls($"http://{appOptions.Host}:{appOptions.Port}");

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {

            }
            app.UseAuthorization();
            app.UseMiddleware<MixedAuthMiddleware>();
            app.UseMiddleware<ExtraInfoMiddleware>();

            app.MapControllers();

            app.Run();
        }
    }
}
