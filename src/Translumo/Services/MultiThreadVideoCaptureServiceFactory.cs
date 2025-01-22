using System.Drawing;

namespace Translumo.Services
{
    public class MultiThreadVideoCaptureServiceFactory
    {
        public IMultiThreadVideoCaptureService GetService(string path, Rectangle rectangle)
        {
            return new FFMpegCoreMultiThreadVideoCaptureService(path, rectangle);
        }
    }
}