using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using TboxWebdav.Server.Modules.Tbox;
using TboxWebdav.Server.Modules.Webdav;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Helpers;
using TboxWebdav.Server.Modules.Webdav.Internal.Props;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;

namespace TboxWebdav.Server.Handlers
{
    /// <summary>
    /// Implementation of the GET and HEAD method.
    /// </summary>
    /// <remarks>
    /// The specification of the WebDAV GET and HEAD methods for collections
    /// can be found in the
    /// <see href="http://www.webdav.org/specs/rfc2518.html#rfc.section.8.4">
    /// WebDAV specification
    /// </see>.
    /// </remarks>
    public class HeadHandler : IWebDavHandler
    {
        private readonly ILogger<HeadHandler> _logger;

        public HeadHandler(ILogger<HeadHandler> logger)
        {
            _logger = logger;
        }
        /// <summary>
        /// Handle a GET or HEAD request.
        /// </summary>
        /// <param name="httpContext">
        /// The HTTP context of the request.
        /// </param>
        /// <param name="store">
        /// Store that is used to access the collections and items.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous GET or HEAD operation. The
        /// task will always return <see langword="true"/> upon completion.
        /// </returns>
        public async Task<WebDavResult> HandleRequestAsync(HttpContext httpContext, IStore store)
        {
            // Obtain request and response
            var request = httpContext.Request;
            var response = httpContext.Response;

            // Determine the requested range
            var range = request.GetRange();

            // Obtain the WebDAV collection
            var entry = await store.GetItemAsync(new Uri(request.GetDisplayUrl()), httpContext).ConfigureAwait(false);
            if (entry == null)
            {
                // Set status to not found
                return new WebDavResult(DavStatusCode.NotFound, "not found");
            }

            if (entry is TboxStoreCollection collection)
            {
                response.SetHeaderValue("Content-Length", "0");
                return new WebDavResult(DavStatusCode.MultiStatus) { HasContent = false };
            }

            // ETag might be used for a conditional request
            string etag = null;

            // Add non-expensive headers based on properties
            var propertyManager = entry.PropertyManager;
            if (propertyManager != null)
            {
                // Add Last-Modified header
                var lastModifiedUtc = (string)await propertyManager.GetPropertyAsync(httpContext, entry, DavGetLastModified<IStoreItem>.PropertyName, true).ConfigureAwait(false);
                if (lastModifiedUtc != null)
                    response.SetHeaderValue("Last-Modified", lastModifiedUtc);

                // Add ETag
                etag = (string)await propertyManager.GetPropertyAsync(httpContext, entry, DavGetEtag<IStoreItem>.PropertyName, true).ConfigureAwait(false);
                if (etag != null)
                    response.SetHeaderValue("Etag", etag);

                // Add type
                var contentType = (string)await propertyManager.GetPropertyAsync(httpContext, entry, DavGetContentType<IStoreItem>.PropertyName, true).ConfigureAwait(false);
                if (contentType != null)
                    response.SetHeaderValue("Content-Type", contentType);

                // Add language
                var contentLanguage = (string)await propertyManager.GetPropertyAsync(httpContext, entry, DavGetContentLanguage<IStoreItem>.PropertyName, true).ConfigureAwait(false);
                if (contentLanguage != null)
                    response.SetHeaderValue("Content-Language", contentLanguage);
            }

            // Do not return the actual item data if ETag matches
            if (etag != null && request.GetHeaderValue("If-None-Match") == etag)
            {
                response.SetHeaderValue("Content-Length", "0");
                return new WebDavResult(DavStatusCode.NotModified);
            }
            // Set the expected content length
            try
            {
                // Add a header that we accept ranges (bytes only)
                response.SetHeaderValue("Accept-Ranges", "bytes");

                // Determine the total length
                var fulllength = long.Parse((string)await propertyManager.GetPropertyAsync(httpContext, entry, DavGetContentLength<IStoreItem>.PropertyName, true).ConfigureAwait(false));
                var length = fulllength;

                // Check if an 'If-Range' was specified
                if (range?.If != null && propertyManager != null)
                {
                    var lastModifiedText = (string)await propertyManager.GetPropertyAsync(httpContext, entry, DavGetLastModified<IStoreItem>.PropertyName, true).ConfigureAwait(false);
                    var lastModified = DateTime.Parse(lastModifiedText, CultureInfo.InvariantCulture);
                    if (lastModified != range.If)
                        range = null;
                }

                long start = 0;
                long end = length - 1;
                // Check if a range was specified
                if (range != null)
                {
                    start = range.Start ?? 0;
                    end = Math.Min(range.End ?? start + 4 * 1024 * 1024, length - 1);
                    length = end - start + 1;

                    // Write the range
                    response.SetHeaderValue("Content-Range", $"bytes {start}-{end} / {fulllength}");

                    // Set status to partial result if not all data can be sent
                    if (length < fulllength)
                        response.SetStatus(DavStatusCode.PartialContent);

                    _logger.Log(LogLevel.Information, $"Content-Range : bytes {start}-{end} / {fulllength}");
                }

                // Set the header, so the client knows how much data is required
                //response.SetHeaderValue("Content-Length", $"{length}");

                // Stream the actual entry
                return new WebDavResult(DavStatusCode.Ok);
            }
            catch (NotSupportedException)
            {
                // If the content length is not supported, then we just skip it
                return new WebDavResult(DavStatusCode.NoContent);
            }
        }
    }
}
