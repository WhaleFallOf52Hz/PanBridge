using Microsoft.AspNetCore.Mvc;
using TboxWebdav.Server.AspNetCore.Routing;
using TboxWebdav.Server.Modules.Webdav;
using TboxWebdav.Server.Modules.Webdav.Internal.Helpers;

namespace TboxWebdav.Server.AspNetCore.Controllers
{
    [ApiController]
    [Route("{*path}")]
    public class TboxStoreController : ControllerBase
    {
        private readonly IWebDavContext _context;
        private readonly IWebDavDispatcher _dispatcher;
        private readonly ILogger<TboxStoreController> _logger;

        public TboxStoreController(IWebDavContext context, IWebDavDispatcher dispatcher, ILogger<TboxStoreController> logger)
        {
            _context = context;
            _dispatcher = dispatcher;
            _logger = logger;

        }

        [HttpOptions]
        public async Task<IActionResult> QueryOptionsAsync(string? path, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"OPTIONS /{path ?? ""}");
            var result = await _dispatcher.OptionsAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);
        }

        [HttpMkCol]
        public async Task<IActionResult> MkColAsync(string path, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"MKCOL /{path ?? ""}");
            var result = await _dispatcher.MkcolAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync(string? path, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"GET /{path ?? ""}");
            var result = await _dispatcher.GetAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> PutAsync(string? path, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"PUT /{path ?? ""}");
            var result = await _dispatcher.PutAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAsync(string? path, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"DELETE /{path ?? ""}");
            var result = await _dispatcher.DeleteAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);
        }

        [HttpPropFind]
        public async Task<IActionResult> PropFindAsync(
            string? path,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"PROPFIND /{path ?? ""}");
            var result = await _dispatcher.PropFindAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);

        }

        [HttpPropPatch]
        public async Task<IActionResult> PropPatchAsync(string? path, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"PROPPATCH /{path ?? ""}");
            var result = await _dispatcher.PropPatchAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);
        }

        [HttpHead]
        public async Task<IActionResult> HeadAsync(string? path, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"HEAD /{path ?? ""}");
            var result = await _dispatcher.HeadAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);
        }
        
        [HttpCopy]
        public async Task<IActionResult> CopyAsync(string? path, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"COPY /{path ?? ""}");
            var result = await _dispatcher.CopyAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);
        }
        
        [HttpMove]
        public async Task<IActionResult> MoveAsync(string? path, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"MOVE /{path ?? ""}");
            var result = await _dispatcher.MoveAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);
        }

        [HttpLock]
        public async Task<IActionResult> LockAsync(string? path, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"LOCK /{path ?? ""}");
            var result = await _dispatcher.LockAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);
        }

        [HttpUnlock]
        public async Task<IActionResult> UnlockAsync(string? path, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"UNLOCK /{path ?? ""}");
            var result = await _dispatcher.UnlockAsync(path ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return ConvertResult(result);
        }

        [NonAction]
        public IActionResult ConvertResult(WebDavResult webDavResult)
        {
            if (webDavResult.HasDocument)
            {
                Response.StatusCode = (int)webDavResult.StatusCode;
                return new FileStreamResult(
                    XmlHelper.GetXmlStream(webDavResult.Document),
                    "text/xml; charset=\"utf-8\""
                )
                { EnableRangeProcessing = false };

            }
            else if (webDavResult.IsFile)
            {
                return new FileStreamResult(
                    webDavResult.FileStream,
                    webDavResult.FileType
                )
                { EnableRangeProcessing = true, FileDownloadName = webDavResult.FileName };
            }
            else if (webDavResult.IsError)
            {
                Response.StatusCode = (int)webDavResult.StatusCode;
                //Response.Body = null;
                return new EmptyResult();
            }            
            else if (webDavResult.HasContent)
            {
                return new ContentResult() { Content = webDavResult.Content, ContentType = "text/html", StatusCode = (int)webDavResult.StatusCode };
            }
            else
            {
                Response.StatusCode = (int)webDavResult.StatusCode;
                //Response.Body = null;
                return new EmptyResult();
            }
        }
    }
}
