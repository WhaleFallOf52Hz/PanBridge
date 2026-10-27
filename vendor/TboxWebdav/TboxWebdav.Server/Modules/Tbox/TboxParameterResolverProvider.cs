using NutzCode.Libraries.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TboxWebdav.Server.Modules.Tbox.Services;
using TboxWebdav.Server.Modules.Webdav.Internal.Helpers;

namespace TboxWebdav.Server.Modules.Tbox
{
    public class TboxParameterResolverProvider
    {
        private readonly TboxUserTokenProvider _userTokenProvider;
        private readonly TboxSpaceCredProvider _credProvider;

        public string UserToken => _userTokenProvider.GetUserToken();

        public TboxParameterResolverProvider(TboxUserTokenProvider userTokenProvider, TboxSpaceCredProvider credProvider)
        {
            _userTokenProvider = userTokenProvider;
            _credProvider = credProvider;
        }

        private string path;
        private long length;

        public void SetPath(string path) {
            this.path = path;
        }

        public void SetLength(long length)
        {
            this.length = length;
        }

        public SeekableWebParameters ParameterResolver(long start)
        {
            if (string.IsNullOrEmpty(UserToken))
                throw new Exception("未登录");
            var cred = _credProvider.GetSpaceCred(UserToken);

            if (cred == null)
                throw new Exception("未登录");

            StringBuilder builder = new StringBuilder();
            builder.Append(TboxService.baseUrl);
            builder.Append($"/api/v1/file/{cred.LibraryId}/{cred.SpaceId}/{path.UrlEncodeByParts()}");
            builder.Append($"?access_token={cred.AccessToken}");

            SeekableWebParameters para = new SeekableWebParameters(new Uri(builder.ToString()));
            para.TimeoutInMilliseconds = 30 * 1000;
            para.HasRange = true;
            para.Method = HttpMethod.Get;
            para.Headers = new System.Collections.Specialized.NameValueCollection();
            para.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            para.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            para.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7");

            para.UserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.17 (KHTML, like Gecko) Chrome/84.0.1312.57 Safari/537.17";

            para.HasRange = true;
            para.RangeStart = start;
            para.RangeEnd = length - 1;
            return para;
        }
    }
}
