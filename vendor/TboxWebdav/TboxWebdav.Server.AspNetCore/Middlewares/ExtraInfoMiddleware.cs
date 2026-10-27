using TboxWebdav.Server.AspNetCore.Models;

namespace TboxWebdav.Server.AspNetCore.Middlewares
{
    public class ExtraInfoMiddleware
    {
        private readonly RequestDelegate _next;

        public ExtraInfoMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Items["CacheSize"] = AppCmdOption.Default.CacheSize.ToString();
            await _next(context);
        }
    }
}
