using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Helpers;
using TboxWebdav.Server.Modules.Webdav.Internal.Props;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;
using TboxWebdav.Server.Modules.Tbox;
using TboxWebdav.Server.Modules.Webdav;
using Microsoft.Extensions.Logging;

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
    public class GetHandler : IWebDavHandler
    {
        private readonly ILogger<GetHandler> _logger;

        public GetHandler(ILogger<GetHandler> logger)
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

            if (entry is TboxStoreCollection collection && collection.FullPath.Trim('/') == "")
            {
                var htmlstr = "<div style=\"width: 400px; height: 200px; margin: auto auto; \">TboxWebdav Server Running!</div>";
                return new WebDavResult(DavStatusCode.Ok) { HasContent = true, Content = htmlstr };
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
                response.SetHeaderValue("Content-Length", $"{length}");

                // Stream the actual entry
                var stream = await entry.GetReadableStreamAsync(httpContext, start, end).ConfigureAwait(false);
                if (stream != null && stream != Stream.Null)
                {
                    //var verifybytes = new byte[100];
                    //var verifycnt = stream.Read(verifybytes, 0, 34);
                    //if (verifycnt == 34)
                    //{
                    //    var verifystr = Encoding.UTF8.GetString(verifybytes);
                    //    if (verifystr == "HTTP/1.0 500 Internal Server Error")
                    //    {
                    //        _logger.Log(LogLevel.Critical, "接收到 HTTP/1.0 500 Internal Server Error");

                    //        return new WebDavResult(DavStatusCode.BadGateway);
                    //    }
                    //}
                    //stream.Position = 0;
                    //await CopyToAsync(stream, response.Stream, 0, length - 1).ConfigureAwait(false);
                    return new WebDavResult(DavStatusCode.Ok, stream)
                    {
                        FileName = entry.Name,
                        FileType = entry.MimeType,
                    };

                }
                else
                {
                    // Set the response
                    response.SetHeaderValue("Content-Length", "0");
                    return new WebDavResult(DavStatusCode.NoContent);
                }
            }
            catch (NotSupportedException)
            {
                // If the content length is not supported, then we just skip it
                return new WebDavResult(DavStatusCode.NoContent);
            }
        }

        private async Task CopyToAsync(Stream src, Stream dest, long start, long? end)
        {
            // Skip to the first offset
            if (start > 0)
            {
                // We prefer seeking instead of draining data
                if (!src.CanSeek)
                    throw new IOException("Cannot use range, because the source stream isn't seekable");

                src.Seek(start, SeekOrigin.Begin);
            }

            // Determine the number of bytes to read
            var bytesToRead = end - start + 1 ?? long.MaxValue;

            // Read in 64KB blocks
            var buffer = new byte[64 * 1024];

            // Copy, until we don't get any data anymore
            while (bytesToRead > 0)
            {
                // Read the requested bytes into memory
                var requestedBytes = (int)Math.Min(bytesToRead, buffer.Length);
                var bytesRead = await src.ReadAsync(buffer, 0, requestedBytes).ConfigureAwait(false);

                // We're done, if we cannot read any data anymore
                if (bytesRead == 0)
                {
                    Console.WriteLine($"bytesToRead: {bytesToRead}");
                    return;
                }


                // Write the data to the destination stream
                await dest.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);

                // Decrement the number of bytes left to read
                bytesToRead -= bytesRead;
            }
        }
    }
}
