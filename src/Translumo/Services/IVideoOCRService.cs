using System;
using System.Threading.Tasks;
using Translumo.Configuration;

namespace Translumo.Services
{
    public interface IVideoOcrService
    {
        Task Start(VideoOcrConfiguration videoOcrConfiguration, IProgress<float> progress);
    }
}