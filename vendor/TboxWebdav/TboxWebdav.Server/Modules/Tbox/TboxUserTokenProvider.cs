using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TboxWebdav.Server.Modules.Tbox.Services;

namespace TboxWebdav.Server.Modules.Tbox
{
    public class TboxUserTokenProvider
    {
        private string userToken;
        private readonly ILogger<TboxUserTokenProvider> _logger;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly JaCookieProvider _cookieProvider;
        private readonly IMemoryCache _mcache;
        private readonly HttpClientFactory _clientFactory;

        public TboxUserTokenProvider(ILogger<TboxUserTokenProvider> logger, IHttpContextAccessor contextAccessor, JaCookieProvider cookieProvider, IMemoryCache mcache, HttpClientFactory clientFactory)
        {
            _logger = logger;
            _contextAccessor = contextAccessor;
            _cookieProvider = cookieProvider;
            _mcache = mcache;
            _clientFactory = clientFactory;
            userToken = _contextAccessor.HttpContext.Items.TryGetValue("UserToken", out var token) ? token.ToString() : string.Empty;
            if (string.IsNullOrEmpty(userToken) && !string.IsNullOrEmpty(_cookieProvider.GetCookie()))
            {
                var cookie = _cookieProvider.GetCookie();
                if (_mcache.TryGetValue($"UserTokenByJaCookie_{cookie}", out token))
                {
                    userToken = token.ToString();
                }
                else
                {
                    userToken = TryJaccountLogin(cookie);
                }
            }
        }

        private string TryJaccountLogin(string cookie)
        {
            var client = _clientFactory.CreateClientWithJaCookie(cookie);
            var res = TboxService.LoginUseJaccount(client);
            if (res.Success)
            {
                _mcache.Set($"UserTokenByJaCookie_{cookie}", res.Result.UserToken, new MemoryCacheEntryOptions()
                {
                    AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(res.Result.ExpiresIn)
                });
                return res.Result.UserToken;
            }
            else
            {
                _logger.LogWarning($"Jaccount login failed: {res.Message}");
                return string.Empty;
            }
        }

        public void SetUserToken(string usertoken)
        {
            userToken = usertoken;
        }

        public string GetUserToken()
        {
            return userToken;
        }
    }
}
