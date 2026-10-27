using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;

namespace TboxWebdav.Server.Modules.Webdav
{
    public class WebDavDispatcher : IWebDavDispatcher
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<WebDavDispatcher> _logger;
        private readonly IStore _store;
        private readonly IServiceProvider _serviceProvider;

        public WebDavDispatcher(IHttpContextAccessor httpContextAccessor, ILogger<WebDavDispatcher> logger, IStore store, IServiceProvider serviceProvider)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _store = store;
            _serviceProvider = serviceProvider;

        }

        public async Task<WebDavResult> OptionsAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.OPTIONS.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error OptionsAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }

        public async Task<WebDavResult> PropFindAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.PROPFIND.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error PropFindAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }

        public async Task<WebDavResult> GetAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.GET.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error GetAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }

        public async Task<WebDavResult> HeadAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.HEAD.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error HeadAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }

        public async Task<WebDavResult> PutAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.PUT.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error PutAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }

        public async Task<WebDavResult> DeleteAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.DELETE.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error DeleteAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }

        public async Task<WebDavResult> LockAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.LOCK.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error LockAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }

        public async Task<WebDavResult> UnlockAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.UNLOCK.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error UnlockAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }

        public async Task<WebDavResult> MkcolAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.MKCOL.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error MkcolAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }

        public async Task<WebDavResult> PropPatchAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.PROPPATCH.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error PropPatchAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }

        public async Task<WebDavResult> MoveAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.MOVE.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error MoveAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }

        public async Task<WebDavResult> CopyAsync(string path, CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredKeyedService<IWebDavHandler>(WebDavRequestMethods.COPY.ToString());
            if (handler == null)
                return new WebDavResult(DavStatusCode.InternalServerError);
            try
            {
                var res = await handler.HandleRequestAsync(_httpContextAccessor.HttpContext, _store);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error CopyAsync: {ex.Message}");
                return new WebDavResult(DavStatusCode.InternalServerError);
            }
        }
    }
}
