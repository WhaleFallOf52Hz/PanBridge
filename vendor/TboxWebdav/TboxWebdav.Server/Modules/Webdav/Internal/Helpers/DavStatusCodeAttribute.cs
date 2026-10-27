using System;

namespace TboxWebdav.Server.Modules.Webdav.Internal.Helpers
{
    [AttributeUsage(AttributeTargets.Field)]
    internal class DavStatusCodeAttribute : Attribute
    {
        public string Description { get; }

        public DavStatusCodeAttribute(string description)
        {
            Description = description;
        }
    }
}
