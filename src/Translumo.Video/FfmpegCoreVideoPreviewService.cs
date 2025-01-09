using System.Drawing;
using FFMpegCore;
using FFMpegCore.Exceptions;
using FFMpegCore.Extensions.System.Drawing.Common;

namespace Translumo.Video;

public class FfmpegCoreVideoPreviewService : IVideoPreviewService
{
    private string? _path;

    public bool IsLoaded => _path != null;

    public TimeSpan LoadVideo(string path)
    {
        try
        {
            var result = FFProbe.Analyse(path);
            _path = path;
            return result.Duration;
        }
        catch (FFMpegException e)
        {
            throw new TranslumoVideoException(e.Message, e);
        }
    }


    public Task<Bitmap> GetVideoAt(TimeSpan time)
    {
        if (!IsLoaded)
        {
            throw new InvalidOperationException($"video not loaded");
        }
        return FFMpegImage.SnapshotAsync(_path!, null, time);
    }
}