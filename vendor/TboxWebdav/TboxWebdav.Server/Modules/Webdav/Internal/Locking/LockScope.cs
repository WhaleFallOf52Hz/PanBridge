using System.Xml.Serialization;

namespace TboxWebdav.Server.Modules.Webdav.Internal.Locking
{
    public enum LockScope
    {
        [XmlEnum("exclusive")]
        Exclusive,

        [XmlEnum("shared")]
        Shared
    }
}
