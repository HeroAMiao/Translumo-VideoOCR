using System.Drawing;

namespace Translumo.Video
{
    public interface IVideoPreviewService
    {
        TimeSpan LoadVideo(string path);
        
        public bool IsLoaded { get; }
        Task<Bitmap> GetVideoAt(TimeSpan time);
    }
}