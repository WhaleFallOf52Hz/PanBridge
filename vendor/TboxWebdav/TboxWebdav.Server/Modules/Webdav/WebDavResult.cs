using System.Net;
using System.Xml.Linq;
using TboxWebdav.Server.Modules.Webdav.Internal;

namespace TboxWebdav.Server.Modules.Webdav
{
    public class WebDavResult
    {
        public WebDavResult(DavStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public WebDavResult(DavStatusCode statusCode, string message) : this(statusCode)
        {
            Message = message;
            IsError = true;
        }

        public WebDavResult(DavStatusCode statusCode, XDocument document) : this(statusCode)
        {
            Document = document;
            HasDocument = true;
        }

        public WebDavResult(DavStatusCode statusCode, Stream fileStream) : this(statusCode)
        {
            FileStream = fileStream;
            IsFile = true;
        }

        public bool IsFile { get; set; }
        public bool IsError { get; set; }
        public bool HasDocument { get; set; }
        public bool HasContent { get; set; }
        public string Message { get; set; }
        public DavStatusCode StatusCode { get; set; }
        public Stream FileStream { get; set; }
        public string Content { get; set; }
        public XDocument Document { get; set; }
        public string FileType { get; set; }
        public string FileName { get; set; }
    }
}
