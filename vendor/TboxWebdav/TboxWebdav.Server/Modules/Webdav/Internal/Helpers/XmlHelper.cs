using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace TboxWebdav.Server.Modules.Webdav.Internal.Helpers
{
    public static class XmlHelper
    {
        public static string GetXmlValue<TEnum>(TEnum value, string defaultValue = null) where TEnum : struct
        {
            // Obtain the member information
            var memberInfo = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
            if (memberInfo == null)
                return defaultValue;

            var xmlEnumAttribute = memberInfo.GetCustomAttribute<XmlEnumAttribute>();
            return xmlEnumAttribute?.Name;
        }

        public static Stream GetXmlStream(XDocument xDocument)
        {
            // Make sure an XML document is specified
            if (xDocument == null)
                throw new ArgumentNullException(nameof(xDocument));

            // Make sure the XML document has a root node
            if (xDocument.Root == null)
                throw new ArgumentException("The specified XML document doesn't have a root node", nameof(xDocument));

            // Obtain the result as an XML document
            var ms = new MemoryStream();
            using (var xmlWriter = XmlWriter.Create(ms, new XmlWriterSettings
            {
                OmitXmlDeclaration = false,
#if DEBUG
                Indent = true,
#else
                    Indent = false,
#endif
                Encoding = new UTF8Encoding(false),
            }))
            {
                // Add the namespaces (Win7 WebDAV client requires them like this)
                xDocument.Root.SetAttributeValue(XNamespace.Xmlns + WebDavNamespaces.DavNsPrefix, WebDavNamespaces.DavNs);
                xDocument.Root.SetAttributeValue(XNamespace.Xmlns + WebDavNamespaces.Win32NsPrefix, WebDavNamespaces.Win32Ns);

                // Write the XML document to the stream
                xDocument.WriteTo(xmlWriter);
            }

            // Flush
            ms.Flush();
            ms.Position = 0;
            return ms;
        }
    }
}
