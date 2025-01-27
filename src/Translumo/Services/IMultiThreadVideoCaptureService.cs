using System;

namespace Translumo.Services
{
    public interface IMultiThreadVideoCaptureService
    {
        public byte[] GetFrameAt(TimeSpan timeSpan);
    }
}