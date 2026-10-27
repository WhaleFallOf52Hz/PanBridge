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
    /// Implementation of the MOVE method.
    /// </summary>
    /// <remarks>
    /// The specification of the WebDAV MOVE method can be found in the
    /// <see href="http://www.webdav.org/specs/rfc2518.html#METHOD_MOVE">
    /// WebDAV specification
    /// </see>.
    /// </remarks>
    public class MoveHandler : IWebDavHandler
    {
        private readonly ILogger<MoveHandler> _logger;
        private readonly IWebDavContext _webDavContext;

        public MoveHandler(ILogger<MoveHandler> logger, IWebDavContext webDavContext)
        {
            _logger = logger;
            _webDavContext = webDavContext;
        }

        /// <summary>
        /// Handle a MOVE request.
        /// </summary>
        /// <param name="httpContext">
        /// The HTTP context of the request.
        /// </param>
        /// <param name="store">
        /// Store that is used to access the collections and items.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous MOVE operation. The task
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

            // We should always move the item from a parent container
            var splitSourceUri = RequestHelper.SplitUri(new Uri(request.GetDisplayUrl()));

            // Obtain source collection
            var sourceCollectionUri = UriHelper.GetPathFromUri(splitSourceUri.CollectionUri);
            var sourceItemUri = UriHelper.Combine(sourceCollectionUri, splitSourceUri.Name);

            // Obtain the destination
            var destinationUri = request.GetDestinationUri();
            if (destinationUri == null)
            {
                // Bad request
                return new WebDavResult(DavStatusCode.BadRequest, "Destination header is missing.");
            }

            // Make sure the source and destination are different
            if (new Uri(request.GetDisplayUrl()).AbsoluteUri.Equals(destinationUri.AbsoluteUri, StringComparison.CurrentCultureIgnoreCase))
            {
                // Forbidden
                return new WebDavResult(DavStatusCode.Forbidden, "Source and destination cannot be the same.");
            }

            // We should always move the item to a parent
            var splitDestinationUri = RequestHelper.SplitUri(destinationUri);

            // Obtain destination collection
            var destinationCollectionUri = UriHelper.GetPathFromUri(splitDestinationUri.CollectionUri);
            var destinationItemUri = UriHelper.Combine(destinationCollectionUri, splitDestinationUri.Name);

            //var topfolder = UriHelper.GetTopFolderFromUri(sourceItemUri);
            //if (topfolder == "他人的分享链接")
            //{
            //    var sourceCollection = await store.GetCollectionAsync(splitSourceUri.CollectionUri, httpContext).ConfigureAwait(false);
            //    var destinationCollection = await store.GetCollectionAsync(splitDestinationUri.CollectionUri, httpContext).ConfigureAwait(false);
            //    var result = await sourceCollection.MoveItemAsync(splitSourceUri.Name, destinationCollection, splitDestinationUri.Name, true, httpContext).ConfigureAwait(false);

            //    response.SetStatus(result.Result);
            //    return true;
            //}

            //if (topfolder == "交大空间")
            //{
            //    var sourceCollection = await store.GetCollectionAsync(splitSourceUri.CollectionUri, httpContext).ConfigureAwait(false);
            //    var destinationCollection = await store.GetCollectionAsync(splitDestinationUri.CollectionUri, httpContext).ConfigureAwait(false);
            //    var result = await sourceCollection.MoveItemAsync(splitSourceUri.Name, destinationCollection, splitDestinationUri.Name, true, httpContext).ConfigureAwait(false);

            //    response.SetStatus(result.Result);
            //    return true;
            //}

            var status = await store.DirectMoveItemAsync(sourceItemUri, destinationItemUri);
            return new WebDavResult(status);
        }

        //private async Task MoveAsync(IStoreCollection sourceCollection, IStoreItem moveItem, IStoreCollection destinationCollection, string destinationName, bool overwrite, IHttpContext httpContext, Uri baseUri, UriResultCollection errors)
        //{
        //    // Determine the new base URI
        //    var subBaseUri = UriHelper.Combine(baseUri, destinationName);

        //    // Obtain the actual item
        //    if (moveItem is IStoreCollection moveCollection && !moveCollection.SupportsFastMove(destinationCollection, destinationName, overwrite, httpContext))
        //    {
        //        // Create a new collection
        //        var newCollectionResult = await destinationCollection.CreateCollectionAsync(destinationName, overwrite, httpContext).ConfigureAwait(false);
        //        if (newCollectionResult.Result != DavStatusCode.Created && newCollectionResult.Result != DavStatusCode.NoContent)
        //        {
        //            errors.AddResult(subBaseUri, newCollectionResult.Result);
        //            return;
        //        }

        //        // Move all sub items
        //        foreach (var entry in await moveCollection.GetItemsAsync(httpContext).ConfigureAwait(false))
        //            await MoveAsync(moveCollection, entry, newCollectionResult.Collection, entry.Name, overwrite, httpContext, subBaseUri, errors).ConfigureAwait(false);

        //        // Delete the source collection
        //        var deleteResult = await sourceCollection.DeleteItemAsync(moveItem.Name, httpContext).ConfigureAwait(false);
        //        if (deleteResult != DavStatusCode.Ok)
        //            errors.AddResult(subBaseUri, newCollectionResult.Result);
        //    }
        //    else
        //    {
        //        // Items should be moved directly
        //        var result = await sourceCollection.MoveItemAsync(moveItem.Name, destinationCollection, destinationName, overwrite, httpContext).ConfigureAwait(false);
        //        if (result.Result != DavStatusCode.Created && result.Result != DavStatusCode.NoContent)
        //            errors.AddResult(subBaseUri, result.Result);
        //    }
        //}

        //private async Task MoveAsync(string sourceCollectionUri, string destinationCollectionUri, string destinationName, IHttpContext httpContext)
        //{
        //    // Determine the new base URI
        //    var subBaseUri = UriHelper.Combine(baseUri, destinationName);

        //    // Items should be moved directly
        //    var result = await sourceCollection.MoveItemAsync(moveItem.Name, destinationCollection, destinationName, overwrite, httpContext).ConfigureAwait(false);
        //    if (result.Result != DavStatusCode.Created && result.Result != DavStatusCode.NoContent)
        //        errors.AddResult(subBaseUri, result.Result);
        //}
    }
}
