using System;
using System.Threading.Tasks;

namespace Translumo.Services
{
    public interface ISequenceVideoCaptureService
    {
        Task SequenceProcess(Func<byte[], Task> consumer);
    }
}