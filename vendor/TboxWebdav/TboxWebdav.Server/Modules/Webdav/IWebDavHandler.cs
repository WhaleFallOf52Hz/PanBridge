using Microsoft.AspNetCore.Http;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;

namespace TboxWebdav.Server.Modules.Webdav
{
    public interface IWebDavHandler
    {
        Task<WebDavResult> HandleRequestAsync(HttpContext httpContext, IStore store);
    }
}
