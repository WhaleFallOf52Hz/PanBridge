using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using TboxWebdav.Server.Modules.Tbox.Models;
using TboxWebdav.Server.Modules.Tbox.Services;
using Teru.Code.Models;
using Teru.Code.Services;

namespace TboxWebdav.Server.Modules.Tbox
{
    public class TboxSpaceCredProvider
    {
        private readonly IMemoryCache _mcache;
        private readonly TboxService _tservice;

        public TboxSpaceCredProvider(IMemoryCache mcache)
        {
            _mcache = mcache;
        }

        public TboxSpaceCred? GetSpaceCred(string userToken)
        {
            TboxSpaceCred? cred = _mcache.Get($"UserSpaceCred_{userToken}") as TboxSpaceCred;
            return cred;
        }            
        
        public void SetSpaceCred(string userToken, TboxSpaceCred cred)
        {
            _mcache.Set($"UserSpaceCred_{userToken}", cred, new MemoryCacheEntryOptions()
            {
                SlidingExpiration = TimeSpan.FromSeconds(cred.ExpiresIn)
            });
        }        
       
    }
}
