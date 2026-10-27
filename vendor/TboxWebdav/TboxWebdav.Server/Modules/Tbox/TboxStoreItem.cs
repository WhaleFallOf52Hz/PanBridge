using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using NutzCode.Libraries.Web;
using NutzCode.Libraries.Web.StreamProvider;
using TboxWebdav.Server.Modules.Tbox.Models;
using TboxWebdav.Server.Modules.Tbox.Services;
using TboxWebdav.Server.Modules.Webdav;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Helpers;
using TboxWebdav.Server.Modules.Webdav.Internal.Locking;
using TboxWebdav.Server.Modules.Webdav.Internal.Props;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;

namespace TboxWebdav.Server.Modules.Tbox
{
    public class TboxStoreItem : IStoreItem
    {
        private readonly ILogger<TboxStoreItem> _logger;
        private TboxFileInfoDto _fileInfo;
        private readonly IWebDavStoreContext _context;
        private readonly IServiceProvider _serviceProvider;
        private readonly TboxService _tbox;
        private readonly TboxUserTokenProvider _tokenProvider;
        private readonly IWebDavContext _webdavContext;

        public TboxStoreItem(ILogger<TboxStoreItem> logger, IWebDavStoreContext context, TboxService tbox, IServiceProvider serviceProvider, TboxUserTokenProvider tokenProvider, IWebDavContext webdavContext)
        {
            _logger = logger;
            _context = context;
            _tbox = tbox;
            _serviceProvider = serviceProvider;
            _tokenProvider = tokenProvider;
            _webdavContext = webdavContext;

            if (webDataProvider == null)
            {
                webDataProvider = new WebDataProvider(20, 4 * 1024, 2, _webdavContext.GetCacheSize() / (4 * 1024));
            }
        }

        public void SetFileInfo(TboxFileInfoDto fileInfo)
        {
            _fileInfo = fileInfo;
        }

        public static PropertyManager<TboxStoreItem> DefaultPropertyManager { get; } = new PropertyManager<TboxStoreItem>(new DavProperty<TboxStoreItem>[]
        {
            // RFC-2518 properties
            new DavCreationDate<TboxStoreItem>
            {
                Getter = (context, item) => item._fileInfo.CreationTime,
                Setter = (context, item, value) =>
                {
                    item._fileInfo.CreationTime = value;
                    return DavStatusCode.Ok;
                }
            },
            new DavDisplayName<TboxStoreItem>
            {
                Getter = (context, item) => item._fileInfo.Name
            },
            new DavGetContentLength<TboxStoreItem>
            {
                Getter = (context, item) => long.Parse(item._fileInfo.Size)
            },
            new DavGetContentType<TboxStoreItem>
            {
                Getter = (context, item) => item.DetermineContentType()
            },
            new DavGetEtag<TboxStoreItem>
            {
                Getter = (context, item) => item.CalculateEtag()
            },
            new DavGetLastModified<TboxStoreItem>
            {
                Getter = (context, item) => item._fileInfo.ModificationTime,
                Setter = (context, item, value) =>
                {
                    item._fileInfo.ModificationTime = value;
                    return DavStatusCode.Ok;
                }
            },
            new DavGetResourceType<TboxStoreItem>
            {
                Getter = (context, item) => null
            },

            // Default locking property handling via the LockingManager
            //new DavLockDiscoveryDefault<TboxStoreItem>(),
            new DavSupportedLockDefault<TboxStoreItem>(),

            // Hopmann/Lippert collection properties
            // (although not a collection, the IsHidden property might be valuable)
            //new DavExtCollectionIsHidden<TboxStoreItem>
            //{
            //    Getter = (context, item) => (item._fileInfo.IsDisplay)
            //},

            // Win32 extensions
            //new Win32CreationTime<TboxStoreItem>
            //{
            //    Getter = (context, item) => item._fileInfo.CreationTimeUtc,
            //    Setter = (context, item, value) =>
            //    {
            //        item._fileInfo.CreationTimeUtc = value;
            //        return DavStatusCode.Ok;
            //    }
            //},
            //new Win32LastAccessTime<TboxStoreItem>
            //{
            //    Getter = (context, item) => item._fileInfo.LastAccessTimeUtc,
            //    Setter = (context, item, value) =>
            //    {
            //        item._fileInfo.LastAccessTimeUtc = value;
            //        return DavStatusCode.Ok;
            //    }
            //},
            //new Win32LastModifiedTime<TboxStoreItem>
            //{
            //    Getter = (context, item) => item._fileInfo.LastWriteTimeUtc,
            //    Setter = (context, item, value) =>
            //    {
            //        item._fileInfo.LastWriteTimeUtc = value;
            //        return DavStatusCode.Ok;
            //    }
            //},
            //new Win32FileAttributes<TboxStoreItem>
            //{
            //    Getter = (context, item) => item._fileInfo.Attributes,
            //    Setter = (context, item, value) =>
            //    {
            //        item._fileInfo.Attributes = value;
            //        return DavStatusCode.Ok;
            //    }
            //}
        });

        public bool IsWritable => _context.IsWritable;
        public string Name => _fileInfo.Name;
        public string MimeType => _fileInfo.ContentType;
        public string UniqueKey => string.Join('/', _fileInfo.Path);
        public string FullPath => string.Join('/', _fileInfo.Path);

        private static WebDataProvider webDataProvider;

        public async Task<Stream> GetReadableStreamAsync(HttpContext httpContext)
        {
            return await GetReadableStreamAsync(httpContext, null, null);
            //var res = _tbox.GetFileStream(FullPath, null, null);
            //return res.Result;
        }

        public async Task<Stream> GetReadableStreamAsync(HttpContext httpContext, long? start, long? end)
        {
            // A path can be overwritten with different bytes or a different length.
            // Reusing its old blocks returns stale data and can make the read loop stall.
            var accountKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_tokenProvider.GetUserToken())));
            var uniqueKey = $"{accountKey}|{FullPath}|{_fileInfo.ETag}|{_fileInfo.Crc64}|{_fileInfo.Size}|{_fileInfo.ModificationTime:O}";
            var provider = _serviceProvider.GetService<TboxParameterResolverProvider>();
            provider.SetPath(FullPath);
            provider.SetLength(long.Parse(_fileInfo.Size));

            SeekableWebStream stream = new SeekableWebStream(uniqueKey, long.Parse(_fileInfo.Size), webDataProvider, provider.ParameterResolver);
            return stream;

            //var res = _tbox.GetFileStream(FullPath, start, end);
            //return res.Result;
        }

        public IPropertyManager PropertyManager => DefaultPropertyManager;
        public ILockingManager LockingManager => _context.LockingManager;

        public async Task<DavStatusCode> CopyAsync(IStoreCollection destination, string name, bool overwrite, HttpContext httpContext)
        {
            var res = _tbox.CopyOrMoveFile(FullPath, UriHelper.Combine(destination.FullPath, name), false);
            if (res.Success)
            {
                return DavStatusCode.Ok;
            }
            else if (res.Message.Contains("FileNotFound"))
            {
                return DavStatusCode.NotFound;
            }
            else
            {
                return DavStatusCode.InternalServerError;
            }
        }

        public override int GetHashCode()
        {
            return _fileInfo.Crc64.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is TboxStoreItem storeItem))
                return false;
            return storeItem.FullPath.Equals(FullPath, StringComparison.CurrentCultureIgnoreCase);
        }

        private string DetermineContentType()
        {
            return _fileInfo.ContentType;
        }

        private string CalculateEtag()
        {
            return _fileInfo.ETag;
        }
    }
}
