using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using TboxWebdav.Server.Modules.Webdav;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Helpers;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;

namespace TboxWebdav.Server.Handlers
{
    /// <summary>
    /// Implementation of the MKCOL method.
    /// </summary>
    /// <remarks>
    /// The specification of the WebDAV MKCOL method can be found in the
    /// <see href="http://www.webdav.org/specs/rfc2518.html#METHOD_MKCOL">
    /// WebDAV specification
    /// </see>.
    /// </remarks>
    public class MkcolHandler : IWebDavHandler
    {
        private readonly ILogger<MkcolHandler> _logger;
        private readonly IWebDavContext _webDavContext;

        public MkcolHandler(ILogger<MkcolHandler> logger, IWebDavContext webDavContext)
        {
            _logger = logger;
            _webDavContext = webDavContext;
        }
        /// <summary>
        /// Handle a MKCOL request.
        /// </summary>
        /// <param name="httpContext">
        /// The HTTP context of the request.
        /// </param>
        /// <param name="store">
        /// Store that is used to access the collections and items.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous MKCOL operation. The task
        /// will always return <see langword="true"/> upon completion.
        /// </returns>
        public async Task<WebDavResult> HandleRequestAsync(HttpContext httpContext, IStore store)
        {
            // Obtain request and response
            var request = httpContext.Request;
            var response = httpContext.Response;

            if (_webDavContext.GetAccessMode() == Models.AppAccessMode.ReadOnly)
            {
                return new WebDavResult(DavStatusCode.Forbidden);
            }

            // The collection must always be created inside another collection
            var splitUri = RequestHelper.SplitUri(new Uri(request.GetDisplayUrl()));

            // Obtain the parent entry
            var collection = await store.GetCollectionAsync(splitUri.CollectionUri, httpContext).ConfigureAwait(false);
            if (collection == null)
            {
                // Source not found
                return new WebDavResult(DavStatusCode.Conflict);
            }

            // Create the collection
            var result = await collection.CreateCollectionAsync(splitUri.Name, false, httpContext).ConfigureAwait(false);

            // Finished
            return new WebDavResult(result);
        }
    }
}
