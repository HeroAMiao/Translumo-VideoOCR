using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Translumo.Utils;
using Translumo.Video;

namespace Translumo.MVVM.ViewModels
{
    public class VideoOcrViewModel : BindableBase, IDisposable
    {
        private readonly IVideoPreviewService _videoPreviewService;
        private readonly ILogger<VideoOcrViewModel> _logger;
        private TimeSpan _duration;

        public TimeSpan CurrentTime => new();

        public TimeSpan Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        private string _path = "no path";
        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        public VideoOcrViewModel(IVideoPreviewService videoPreviewService, ILogger<VideoOcrViewModel> logger)
        {
            _videoPreviewService = videoPreviewService;
            _logger = logger;
        }

        public void OnLoadClicked()
        {
            if (!File.Exists(_path))
            {
                MessageBox.Show(LocalizationManager.GetValue("Str.Stages.NotAValidVideoFile"));
                return;
            }
            try
            {
                var duration = _videoPreviewService.LoadVideo(_path);
                Duration = duration;
            }
            catch (TranslumoVideoException e)
            {
                MessageBox.Show(e.Message);
                _logger.Log(LogLevel.Error, e, "Load video error");
            }
        }


        public void Dispose()
        {
            LocalizationManager.ReleaseChangedValuesCallbacks(this);
        }
    }
}