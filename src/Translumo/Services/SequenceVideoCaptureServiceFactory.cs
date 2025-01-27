using System.Drawing;

namespace Translumo.Services
{
    public class SequenceVideoCaptureServiceFactory
    {
        public ISequenceVideoCaptureService Create(string path, Rectangle rectangle, int intervalMs)
        {
            return new FFMpegSequenceVideoCaptureService(path, rectangle, intervalMs);
        }
    }
}