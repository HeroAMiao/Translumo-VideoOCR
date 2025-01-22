using System;

namespace Translumo.Services
{
    public interface IMultiThreadVideoCaptureService : IDisposable
    {
        public byte[] GetFrameAt(TimeSpan timeSpan);

        public string Test();
    }
}