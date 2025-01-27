using System;
using System.Threading.Tasks;
using Translumo.Configuration;
using Translumo.Utils;

namespace Translumo.Services
{
    public interface IVideoOcrService
    {
        Task Start(VideoOcrConfiguration videoOcrConfiguration, IProgress<VideoOcrProgress> progress);
    }
}