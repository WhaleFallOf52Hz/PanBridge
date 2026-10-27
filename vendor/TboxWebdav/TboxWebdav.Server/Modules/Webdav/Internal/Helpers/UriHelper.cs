using System;
using System.Text;

namespace TboxWebdav.Server.Modules.Webdav.Internal.Helpers
{
    public static class UriHelper
    {
        public static string BuildQuery(Dictionary<string, string> query)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("?");
            foreach (var kvp in query)
            {
                sb.Append(kvp.Key);
                if (kvp.Value != null)
                {
                    sb.Append("=");
                    sb.Append(kvp.Value);
                }
                sb.Append("&");
            }
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        public static string Combine(string baseUri, string path)
        {
            var uriText = baseUri;
            if (uriText.EndsWith("/"))
                uriText = uriText.Substring(0, uriText.Length - 1);
            return $"{uriText}/{path}";
        }

        public static Uri Combine(Uri baseUri, string path, bool isdir = false)
        {
            var uriText = baseUri.OriginalString;
            if (uriText.EndsWith("/"))
                uriText = uriText.Substring(0, uriText.Length - 1);
            if (isdir)
                return new Uri($"{uriText}/{path}/", UriKind.Absolute);
            else
                return new Uri($"{uriText}/{path}", UriKind.Absolute);
        }

        public static string UrlEncode(this string url)
        {
            return Uri.EscapeDataString(url);
        }

        public static string Connect(this IEnumerable<string> iterator, string separator)
        {
            StringBuilder s = new StringBuilder();
            var it = iterator.GetEnumerator();
            it.MoveNext();
            bool next;
            do
            {

                s.Append(it.Current);
                next = it.MoveNext();
                if (next)
                    s.Append(separator);

            } while (next);

            return s.ToString();
        }

        public static string UrlEncodeByParts(this string url)
        {
            return url.Split('/')
                       .Select(s => s.UrlEncode())
                       .Connect("/");
        }

        public static string ToEncodedString(Uri entryUri)
        {
            return entryUri
                .AbsoluteUri
                .Replace("#", "%23")
                .Replace("[", "%5B")
                .Replace("]", "%5D");
        }

        public static string ToEncodedString(string entryUri)
        {
            return entryUri
                .Replace("#", "%23")
                .Replace("[", "%5B")
                .Replace("]", "%5D");
        }

        public static string GetDecodedPath(Uri uri)
        {
            return uri.LocalPath + Uri.UnescapeDataString(uri.Fragment);
        }

        public static string GetPathFromUri(Uri uri)
        {
            // Determine the path
            var requestedPath = GetDecodedPath(uri);

            // Return the combined path
            return requestedPath;
        }

        public static string GetTopFolderFromUri(Uri uri)
        {
            var folders = uri.LocalPath.Split('/');
            if (folders.Length >= 2)
                return folders[1];
            else
                return null;
        }

        public static string GetTopFolderFromUri(string uri)
        {
            var folders = uri.Split('/');
            if (folders.Length >= 2)
                return folders[1];
            else
                return null;
        }

        public static string RemoveTopFolder(string path)
        {
            var folders = path.Split('/').ToList();
            if (folders.Count >= 2)
                folders.RemoveAt(1);
            return string.Join('/', folders);
        }

        public static string GetParentPath(this string path)
        {
            var s = path.Split('/');
            var p = string.Join("/", s.Take(s.Length - 1));
            return p == "" ? "/" : p;
        }
    }
}
