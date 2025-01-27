using System;
using System.Drawing;
using System.IO;
using System.Linq;
using FFMpegCore;
using FFMpegCore.Pipes;

namespace Translumo.Services
{
    public class FFMpegCoreMultiThreadVideoCaptureService : IMultiThreadVideoCaptureService
    {
        private readonly IMediaAnalysis _result;
        private readonly Rectangle _rectangle;
        private readonly string _path;
        private readonly int _videoStreamIndex;

        public FFMpegCoreMultiThreadVideoCaptureService(string path, Rectangle rectangle)
        {
            _path = path;
            _result = FFProbe.Analyse(path);
            
            var primaryVideoStream = _result.PrimaryVideoStream;
            if (primaryVideoStream == null)
            {
                var videoStream = _result.VideoStreams.FirstOrDefault();
                _videoStreamIndex = videoStream?.Index ?? 0;
            }
            else
            {
                _videoStreamIndex = primaryVideoStream.Index;
            }
            _rectangle = rectangle;
        }

        public byte[] GetFrameAt(TimeSpan timeSpan)
        {
            using (var outputStream = new MemoryStream())
            {
                FFMpegArguments
                    .FromFileInput(_path, false, op => op.Seek(timeSpan))
                    .OutputToPipe(new StreamPipeSink(outputStream), options => options
                        .SelectStream(_videoStreamIndex)
                        .ForcePixelFormat("rgb24")
                        .WithFrameOutputCount(1)
                        .WithVideoCodec("tiff")
                        .WithCustomArgument($"-vf crop={_rectangle.Width}:{_rectangle.Height}:{_rectangle.X}:{_rectangle.Y}")
                        .ForceFormat("image2"))
                    .ProcessSynchronously();
                return outputStream.ToArray();
            }
        }

    }
}