using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore.Pipes;

namespace Translumo.Utils
{
    public class TiffSeparatorSink : IPipeSink
    {
        private readonly Func<byte[], Task> _tiffConsumer;
        private readonly TiffStreamSeparator _separator;

        public TiffSeparatorSink(Func<byte[], Task> tiffConsumer)
        {
            _tiffConsumer = tiffConsumer;
            _separator = new TiffStreamSeparator();
        }

        public async Task ReadAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            while (true)
            {
                var tryReadOneTiff = await _separator.TryReadOneTiff(inputStream, cancellationToken).ConfigureAwait(false);
                if (tryReadOneTiff == false)
                {
                    break;
                }
                var tiff = _separator.GetTiff();
                await _tiffConsumer(tiff);
            }
        }

        public string GetFormat()
        {
            return "";
        }
        
        
    }
}