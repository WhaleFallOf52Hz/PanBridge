using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NutzCode.Libraries.Web.StreamProvider;

namespace NutzCode.Libraries.Web
{
    public class SeekableWebStream : Stream
    {

        private long _position;
        private long _totallength;
        private string _key;
        private Func<long, SeekableWebParameters> _resolver;
        private WebDataProvider _provider;

        public SeekableWebStream(string key, long totallength, WebDataProvider provider, Func<long, SeekableWebParameters> webParameterResolver)
        {
            _resolver = webParameterResolver;
            _provider = provider;
            _key = key;
            _totallength = totallength;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _totallength;
        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException(nameof(Position));
                _position = value;
            }
        }
        public override void Flush()
        {
            // No-op
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition = origin switch
            {
                SeekOrigin.Begin => 0 + offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _totallength + offset,
                _ => throw new ArgumentException("Invalid SeekOrigin.")
            };

            if (newPosition < 0 || newPosition > _totallength)
                throw new ArgumentOutOfRangeException(nameof(offset));

            _position = newPosition;
            return _position;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Task.Run(() => ReadAsync(buffer, offset, count, new CancellationToken())).Result;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentException("Invalid offset or count.");

            int bytesToRead = (int)Math.Min(count, Length - _position);
            if (bytesToRead == 0)
                return 0;

            int bytesRead = await _provider.Read(_key, _resolver, Length, _position, buffer, offset, bytesToRead, cancellationToken);
            _position += bytesRead;
            return bytesRead;
        }
    }
}
