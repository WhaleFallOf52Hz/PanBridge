using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TboxWebdav.Server.Modules.Tbox
{
    public class JaCookieProvider
    {
        private string cookie;
        private readonly IHttpContextAccessor _contextAccessor;

        public JaCookieProvider(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
            cookie = _contextAccessor.HttpContext.Items.TryGetValue("JaCookie", out var c) ? c.ToString() : string.Empty;
        }

        public void SetCookie(string c)
        {
            cookie = c;
        }

        public string GetCookie()
        {
            return cookie;
        }
    }
}
