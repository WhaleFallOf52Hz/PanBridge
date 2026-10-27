using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using TboxWebdav.Server.Modules.Webdav;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Helpers;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;
using UriHelper = TboxWebdav.Server.Modules.Webdav.Internal.Helpers.UriHelper;

namespace TboxWebdav.Server.Handlers
{
    /// <summary>
    /// Implementation of the DELETE method.
    /// </summary>
    /// <remarks>
    /// The specification of the WebDAV DELETE method can be found in the
    /// <see href="http://www.webdav.org/specs/rfc2518.html#METHOD_DELETE">
    /// WebDAV specification
    /// </see>.
    /// </remarks>
    public class DeleteHandler : IWebDavHandler
    {
        private readonly ILogger<DeleteHandler> _logger;
        private readonly IWebDavContext _webDavContext;

        public DeleteHandler(ILogger<DeleteHandler> logger, IWebDavContext webDavContext)
        {
            _logger = logger;
            _webDavContext = webDavContext;
        }

        /// <summary>
        /// Handle a DELETE request.
        /// </summary>
        /// <param name="httpContext">
        /// The HTTP context of the request.
        /// </param>
        /// <param name="store">
        /// Store that is used to access the collections and items.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous DELETE operation. The task
        /// will always return <see langword="true"/> upon completion.
        /// </returns>
        public async Task<WebDavResult> HandleRequestAsync(HttpContext httpContext, IStore store)
        {
            // Obtain request and response
            var request = httpContext.Request;
            var response = httpContext.Response;

            if (_webDavContext.GetAccessMode() == Models.AppAccessMode.ReadOnly
                || _webDavContext.GetAccessMode() == Models.AppAccessMode.NoDelete)
            {
                return new WebDavResult(DavStatusCode.Forbidden);
            }

            // We should always remove the item from a parent container
            var splitUri = RequestHelper.SplitUri(new Uri(request.GetDisplayUrl()));

            // Obtain parent collection
            var parentCollectionUri = UriHelper.GetPathFromUri(splitUri.CollectionUri);

            // Obtain the item that actually is deleted
            var deleteItemUri = UriHelper.Combine(parentCollectionUri, splitUri.Name);

            var topfolder = UriHelper.GetTopFolderFromUri(deleteItemUri);

            //if (topfolder == "他人的分享链接")
            //{
            //    return new WebDavResult(DavStatusCode.Forbidden);
            //}

            //if (topfolder == "交大空间")
            //{
            //    return new WebDavResult(DavStatusCode.Forbidden);
            //}

            // Delete item
            var status = await store.DirectDeleteItemAsync(deleteItemUri);

            return new WebDavResult(status);
        }

        //private async Task<DavStatusCode> DeleteItemAsync(IStoreCollection collection, string name, IStoreItem deleteItem, HttpContext httpContext, Uri baseUri)
        //{
        //    //if (deleteItem is IStoreCollection deleteCollection)
        //    //{
        //    //    // Determine the new base URI
        //    //    var subBaseUri = UriHelper.Combine(baseUri, name);

        //    //    // Delete all entries first
        //    //    foreach (var entry in await deleteCollection.GetItemsAsync(httpContext).ConfigureAwait(false))
        //    //        await DeleteItemAsync(deleteCollection, entry.Name, entry, httpContext, subBaseUri).ConfigureAwait(false);
        //    //}

        //    // Attempt to delete the item
        //    return await collection.DeleteItemAsync(name, httpContext).ConfigureAwait(false);
        //}
    }
}
