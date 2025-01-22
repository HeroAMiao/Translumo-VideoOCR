using System;
using System.Drawing;
using System.IO;
using System.Linq;
using FFMpegCore;
using FFMpegCore.Arguments;
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
                // 调用 FFmpeg 处理视频帧
                FFMpegArguments
                    .FromFileInput(_path, false, op => op.Seek(timeSpan)) // 输入视频文件
                    .OutputToPipe(new StreamPipeSink(outputStream), options => options
                        .SelectStream(_videoStreamIndex)
                        .ForcePixelFormat("rgb24")
                        .WithFrameOutputCount(1)
                        .WithVideoCodec("tiff") // 设置输出为 TIFF 格式
                        .WithCustomArgument($"-vf crop={_rectangle.Width}:{_rectangle.Height}:{_rectangle.X}:{_rectangle.Y}") // 裁剪指定区域
                        .ForceFormat("image2")) // 强制输出图像格式
                    .ProcessSynchronously();
                // 将内存流转换为 byte 数组
                return outputStream.ToArray();
            }
        }

        public string Test()
        {
            using (var outputStream = new MemoryStream())
            {
                var timeSpan = TimeSpan.FromSeconds(1);
                // 调用 FFmpeg 处理视频帧
                var a = FFMpegArguments
                    .FromFileInput(_path, false, op => op.Seek(timeSpan)) // 输入视频文件
                    .OutputToPipe(new StreamPipeSink(outputStream), options => options
                        .SelectStream(_videoStreamIndex)
                        .ForcePixelFormat("rgb24")
                        .WithFrameOutputCount(1)
                        .WithVideoCodec("tiff") // 设置输出为 TIFF 格式
                        .WithCustomArgument(
                            $"-vf crop={_rectangle.Width}:{_rectangle.Height}:{_rectangle.X}:{_rectangle.Y}") // 裁剪指定区域
                        .ForceFormat("image2")) // 强制输出图像格式
                    .Arguments;
                return a;
            }
        }

        public void Dispose()
        {
        }
    }
}