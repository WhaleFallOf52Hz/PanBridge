using System.Text;
using TboxWebdav.Server.AspNetCore.Models;

public class MixedAuthMiddleware
{
    private readonly RequestDelegate _next;
    private const string AuthenticationHeaderName = "Authorization";

    public MixedAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (AppCmdOption.Default.AuthMode == AppAuthMode.None)
        {
            if (AppCmdOption.Default.UserToken != null)
            {
                context.Items["UserToken"] = AppCmdOption.Default.UserToken;
                context.Items["AccessMode"] = AppCmdOption.Default.AccessMode.ToString();
                await _next(context);
                return;
            }
            else if (AppCmdOption.Default.Cookie != null)
            {
                context.Items["JaCookie"] = AppCmdOption.Default.Cookie;
                context.Items["AccessMode"] = AppCmdOption.Default.AccessMode.ToString();
                await _next(context);
                return;
            }
            await SendUnauthorizedResponse(context);
            return;
        }
        if (!context.Request.Headers.TryGetValue(AuthenticationHeaderName, out var authHeaders))
        {
            await SendUnauthorizedResponse(context);
            return;
        }

        var authHeader = authHeaders.FirstOrDefault();

        if (authHeader == null || authHeader.ToString().Length == 0)
        {
            await SendUnauthorizedResponse(context);
            return;
        }

        if (!authHeader.ToString().StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            await SendUnauthorizedResponse(context);
            return;
        }

        var encodedUsernamePassword = authHeader.ToString().Substring("Basic ".Length).Trim();
        var encoding = Encoding.GetEncoding("iso-8859-1");
        var usernamePassword = encoding.GetString(Convert.FromBase64String(encodedUsernamePassword));

        var username = usernamePassword.Split(':')[0];
        var password = usernamePassword.Split(':')[1];

        if ((AppCmdOption.Default.AuthMode == AppAuthMode.UserToken || AppCmdOption.Default.AuthMode == AppAuthMode.Mixed) && IsValidUserToken(password))
        {
            context.Items["UserToken"] = password;
            context.Items["AccessMode"] = AppCmdOption.Default.AccessMode.ToString();
            await _next(context);
            return;
        }
        if ((AppCmdOption.Default.AuthMode == AppAuthMode.JaCookie || AppCmdOption.Default.AuthMode == AppAuthMode.Mixed) && IsValidJaCookie(password))
        {
            context.Items["JaCookie"] = password;
            context.Items["AccessMode"] = AppCmdOption.Default.AccessMode.ToString();
            await _next(context);
            return;
        }
        if (AppCmdOption.Default.AuthMode == AppAuthMode.Custom || AppCmdOption.Default.AuthMode == AppAuthMode.Mixed)
        {
            var user = AppCmdOption.Default.Users.FirstOrDefault(u => u.UserName != null && u.UserName == username && (u.Password == null || (u.Password != null && u.Password == password)));
            if (user != null)
            {
                if (user.UserToken != null)
                {
                    context.Items["UserToken"] = user.UserToken;
                    context.Items["AccessMode"] = user.AccessMode.ToString();
                    await _next(context);
                    return;
                }
                else if (user.Cookie != null)
                {
                    context.Items["JaCookie"] = user.Cookie;
                    context.Items["AccessMode"] = user.AccessMode.ToString();
                    await _next(context);
                    return;
                }
            }
        }
        await SendUnauthorizedResponse(context);
    }

    private bool IsValidUserToken(string password)
    {
        return password.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')) && password.Length == 128;
    }
    
    private bool IsValidJaCookie(string password)
    {
        var test = Convert.TryFromBase64String(password, new Span<byte>(new byte[password.Length]), out var _);
        return test;
    }

    private async Task SendUnauthorizedResponse(HttpContext context)
    {
        context.Response.StatusCode = 401; // Unauthorized
        context.Response.Headers["WWW-Authenticate"] = "Basic";
        //await context.Response.WriteAsync("Unauthorized");
    }
}