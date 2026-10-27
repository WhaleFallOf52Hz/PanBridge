using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TboxWebdav.Server.Modules
{
    public class HttpClientFactory
    {
        private readonly ILogger<HttpClientFactory> _logger;

        public HttpClientFactory(ILogger<HttpClientFactory> logger)
        {
            _logger = logger;
        }

        public HttpClient CreateClient()
        {
            var client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = true, AutomaticDecompression = System.Net.DecompressionMethods.All});
            
            return client;
        }        
        
        public HttpClient CreateClientWithJaCookie(string cookie)
        {
            var cc = new CookieContainer();
            cc.Add(new Cookie("JAAuthCookie", cookie, "/jaccount", "jaccount.sjtu.edu.cn"));
            var client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = true, AutomaticDecompression = System.Net.DecompressionMethods.All, UseCookies = true, CookieContainer = cc });
            
            return client;
        }
    }
}
