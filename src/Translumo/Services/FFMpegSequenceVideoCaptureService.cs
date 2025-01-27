using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using FFMpegCore;
using Translumo.Utils;

namespace Translumo.Services
{
    public class FFMpegSequenceVideoCaptureService : ISequenceVideoCaptureService
    {
        private readonly string _path;
        private readonly Rectangle _rectangle;
        private readonly int _intervalMs;
        private readonly int _videoStreamIndex;

        public FFMpegSequenceVideoCaptureService(string path, Rectangle rectangle, int intervalMs)
        {
            _path = path;
            _rectangle = rectangle;
            _intervalMs = intervalMs;

            var result = FFProbe.Analyse(path);
            Duration = result.Duration;
            var primaryVideoStream = result.PrimaryVideoStream;
            if (primaryVideoStream == null)
            {
                var videoStream = result.VideoStreams.FirstOrDefault();
                _videoStreamIndex = videoStream?.Index ?? 0;
            }
            else
            {
                _videoStreamIndex = primaryVideoStream.Index;
            }
            _rectangle = rectangle;
        }

        public Task SequenceProcess(Func<byte[], Task> consumer)
        {
            return FFMpegArguments.FromFileInput(_path, false)
                .OutputToPipe(new TiffSeparatorSink(consumer), options => options
                    .SelectStream(_videoStreamIndex)
                    .ForcePixelFormat("rgb24")
                    .WithFramerate(1000.0 / _intervalMs)
                    .WithVideoCodec("tiff")
                    .WithCustomArgument(
                        $"-vf crop={_rectangle.Width}:{_rectangle.Height}:{_rectangle.X}:{_rectangle.Y}")
                    .ForceFormat("image2pipe")) // 强制输出图像格式
                .ProcessAsynchronously();
        }

        public TimeSpan Duration { get; }
    }
}