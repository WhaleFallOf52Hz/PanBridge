using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TboxWebdav.Server.Models;

namespace TboxWebdav.Server.Modules.Webdav
{
    public interface IWebDavContext
    {
        AppAccessMode GetAccessMode();
        int GetCacheSize();
    }
}
