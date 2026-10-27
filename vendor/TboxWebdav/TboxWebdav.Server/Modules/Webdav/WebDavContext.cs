using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TboxWebdav.Server.Models;
using TboxWebdav.Server.Modules.Tbox;

namespace TboxWebdav.Server.Modules.Webdav
{
    public class WebDavContext : IWebDavContext
    {
        private AppAccessMode accessMode;
        private int cacheSize;
        //private bool deletePermanently;
        private readonly ILogger<WebDavContext> _logger;
        private readonly IHttpContextAccessor _contextAccessor;

        public WebDavContext(ILogger<WebDavContext> logger, IHttpContextAccessor contextAccessor)
        {
            _logger = logger;
            _contextAccessor = contextAccessor;

            accessMode = Enum.Parse<AppAccessMode>(_contextAccessor.HttpContext.Items["AccessMode"] as string);
            cacheSize = int.Parse(_contextAccessor.HttpContext.Items["CacheSize"] as string);
        }

        public AppAccessMode GetAccessMode()
        {
            return accessMode;
        }

        public int GetCacheSize()
        {
            return cacheSize;
        }
    }
}
