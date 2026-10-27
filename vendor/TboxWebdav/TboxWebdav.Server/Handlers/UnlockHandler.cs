using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System.Threading.Tasks;
using TboxWebdav.Server.Modules.Webdav;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Helpers;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;

namespace TboxWebdav.Server.Handlers
{
    /// <summary>
    /// Implementation of the UNLOCK method.
    /// </summary>
    /// <remarks>
    /// The specification of the WebDAV UNLOCK method can be found in the
    /// <see href="http://www.webdav.org/specs/rfc2518.html#METHOD_UNLOCK">
    /// WebDAV specification
    /// </see>.
    /// </remarks>
    public class UnlockHandler : IWebDavHandler
    {
        /// <summary>
        /// Handle a UNLOCK request.
        /// </summary>
        /// <param name="httpContext">
        /// The HTTP context of the request.
        /// </param>
        /// <param name="store">
        /// Store that is used to access the collections and items.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous UNLOCK operation. The task
        /// will always return <see langword="true"/> upon completion.
        /// </returns>
        public async Task<WebDavResult> HandleRequestAsync(HttpContext httpContext, IStore store)
        {
            // Obtain request and response
            var request = httpContext.Request;
            var response = httpContext.Response;

            // Obtain the lock-token
            var lockToken = request.GetLockToken();

            // Obtain the WebDAV item
            var item = await store.GetItemAsync(new Uri(request.GetDisplayUrl()), httpContext).ConfigureAwait(false);
            if (item == null)
            {
                // Set status to not found
                return new WebDavResult(DavStatusCode.PreconditionFailed);
            }

            // Check if we have a lock manager
            var lockingManager = item.LockingManager;
            if (lockingManager == null)
            {
                // Set status to not found
                return new WebDavResult(DavStatusCode.PreconditionFailed);
            }

            // Perform the lock
            var result = lockingManager.Unlock(item, lockToken);

            // Send response
            return new WebDavResult(result);
        }
    }
}
