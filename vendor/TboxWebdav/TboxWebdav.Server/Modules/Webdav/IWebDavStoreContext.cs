using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TboxWebdav.Server.Modules.Webdav.Internal.Locking;

namespace TboxWebdav.Server.Modules.Webdav
{
    public interface IWebDavStoreContext
    {
        bool IsWritable { get; }
        ILockingManager LockingManager { get; }
    }
}
