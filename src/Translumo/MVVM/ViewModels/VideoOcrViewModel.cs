using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
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
        private TimeSpan _currentTime;
        private Task<Bitmap> _previewTask;
        private string _path = "no path";
        private Bitmap _bitmap;

        public TimeSpan CurrentTime
        {
            get => _currentTime;
            set {
                SetProperty(ref _currentTime, value);
                OnCurrentTimeChanged();
            }
        }

        public Bitmap PreviewBitmap
        {
            get => _bitmap;
            set => SetProperty(ref _bitmap, value);
        }
        

        public int ProgressValue
        {
            get
            {
                if (_duration.Ticks == 0)
                {
                    return 0;
                }

                return (int)(_currentTime.Ticks * 100 / _duration.Ticks);
            }
            set
            {
                if (_duration.Ticks == 0)
                {
                    return;
                }

                var timeSpan = new TimeSpan(value * _duration.Ticks / 100);
                SetProperty(ref _currentTime, timeSpan);
                OnPropertyChanged(nameof(CurrentTime));
                OnCurrentTimeChanged();
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

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
                CurrentTime = new TimeSpan(0);
                _videoPreviewService.GetVideoAt(CurrentTime);
            }
            catch (TranslumoVideoException e)
            {
                MessageBox.Show(e.Message);
                _logger.Log(LogLevel.Error, e, "Load video error");
            }
        }
        

        private void OnCurrentTimeChanged()
        {
            if (_previewTask == null || _previewTask.IsCompleted)
            {
                LoadPreviewAt(CurrentTime);
            }
        }

        private void LoadPreviewAt(TimeSpan currentTime)
        {
            _previewTask = _videoPreviewService.GetVideoAt(currentTime);
            _previewTask.ContinueWith(v => OnPreviewBitmapLoaded(currentTime, v), TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnPreviewBitmapLoaded(TimeSpan time, Task<Bitmap> obj)
        {
            var bitmap = obj.Result;
            var old = PreviewBitmap;
            PreviewBitmap = bitmap;
            try
            {
                if (_currentTime != time)
                {
                    LoadPreviewAt(_currentTime);
                }
            }
            finally
            {
                old?.Dispose();
            }
            
        }


        public void Dispose()
        {
            LocalizationManager.ReleaseChangedValuesCallbacks(this);
        }
    }
}