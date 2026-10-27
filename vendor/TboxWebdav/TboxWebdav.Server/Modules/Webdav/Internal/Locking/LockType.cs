using System.Xml.Serialization;

namespace TboxWebdav.Server.Modules.Webdav.Internal.Locking
{
    public enum LockType
    {
        [XmlEnum("write")]
        Write
    }
}
