using System.Drawing;

namespace Translumo.Configuration
{
    public class VideoOcrConfiguration
    {
        public string VideoPath;
        public Rectangle Rectangle;
        public int Interval;
        public int StableFrameCount;
        public double ChangeThreshold;
    }
}