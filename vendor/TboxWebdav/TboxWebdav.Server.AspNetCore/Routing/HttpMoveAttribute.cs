using Microsoft.AspNetCore.Mvc.Routing;
using System.Diagnostics.CodeAnalysis;

namespace TboxWebdav.Server.AspNetCore.Routing
{
    /// <summary>
    /// The WebDAV HTTP MOVE method
    /// </summary>
    public class HttpMoveAttribute : HttpMethodAttribute
    {
        private static readonly IEnumerable<string> _supportedMethods = new[] { "MOVE" };

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpMoveAttribute"/> class.
        /// </summary>
        public HttpMoveAttribute()
            : base(_supportedMethods)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpMoveAttribute"/> class.
        /// </summary>
        /// <param name="template">The route template. May not be null.</param>
        public HttpMoveAttribute([NotNull] string template)
            : base(_supportedMethods, template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));
        }
    }
}
