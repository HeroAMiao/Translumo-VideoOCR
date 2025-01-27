using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Translumo.Configuration;
using Translumo.Services;
using Translumo.Utils;
using Translumo.Video;
using MessageBox = System.Windows.Forms.MessageBox;
using Point = System.Windows.Point;

namespace Translumo.MVVM.ViewModels
{
    public class VideoOcrViewModel : BindableBase, IDisposable
    {
        private readonly IVideoPreviewService _videoPreviewService;
        private readonly ILogger<VideoOcrViewModel> _logger;
        private readonly IServiceProvider _serviceProvider;
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

        public int Interval
        {
            get => _interval;
            set => SetProperty(ref _interval, value);
        }

        public int MinFrame
        {
            get => _minFrame;
            set => SetProperty(ref _minFrame, value);
        }

        public double ChangeThreshold
        {
            get => _changeThreshold;
            set => SetProperty(ref _changeThreshold, value);
        }

        public bool TwoPassOcr
        {
            get => _twoPassOcr;
            set => SetProperty(ref _twoPassOcr, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        public VideoOcrProgress Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private VideoOcrProgress _progress;
        private bool _isProcessing;
        private bool _twoPassOcr = true;
        private int _interval = 300;
        private int _minFrame = 3;
        private double _changeThreshold = 0.1;

        private Rect _selectedArea;
        public Rect SelectedArea
        {
            get => _selectedArea;
            set
            {
                SetProperty(ref _selectedArea, value);
            }
        }


        public int ProgressMax
        {
            get => (int)_duration.TotalMilliseconds;
        }
        
        public int ProgressValue
        {
            get
            {
                return (int)_currentTime.TotalMilliseconds;
            }
            set
            {
                var timeSpan = TimeSpan.FromMilliseconds(value);
                SetProperty(ref _currentTime, timeSpan);
                OnPropertyChanged(nameof(CurrentTime));
                OnCurrentTimeChanged();
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                SetProperty(ref _duration, value);
                OnPropertyChanged(nameof(ProgressMax));
            }
        }

        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        public VideoOcrViewModel(IVideoPreviewService videoPreviewService, ILogger<VideoOcrViewModel> logger, IServiceProvider serviceProvider)
        {
            _videoPreviewService = videoPreviewService;
            _logger = logger;
            _serviceProvider = serviceProvider;
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
        
        
        public Rectangle CalculateChopRectangle(double width, double height)
        {
            var bitmap = _bitmap;
            var area = SelectedArea;
            Point bitmapDisplaySize;
            if (bitmap.Width / (double)bitmap.Height < width / height)
            {
                //bitmap is thinner, padding is horizontal
                bitmapDisplaySize = new Point(height / bitmap.Height * bitmap.Width, height);
            }
            else
            {
                //canvas is thinner, padding is vertical
                bitmapDisplaySize = new Point(1, width / bitmap.Width * bitmap.Height);
            }
            var padding = new Point((width - bitmapDisplaySize.X) / 2, (height - bitmapDisplaySize.Y) / 2);
            var rectXInBitmap = (area.Left - padding.X) / bitmapDisplaySize.X * bitmap.Width;
            var rectYInBitmap = (area.Top - padding.Y) / bitmapDisplaySize.Y * bitmap.Height;
            var rectWidthInBitmap = area.Width / bitmapDisplaySize.X * bitmap.Width;
            var rectHeightInBitmap = area.Height / bitmapDisplaySize.Y * bitmap.Height;
            
            var finalX = (int)Math.Round(Math.Max(rectXInBitmap, 0));
            var finalY = (int)Math.Round(Math.Max(rectYInBitmap, 0));
            var rectangle = new Rectangle
            {
                X = finalX,
                Y = finalY,
                Width = (int)Math.Round(Math.Min(rectWidthInBitmap, bitmap.Width - finalX)),
                Height = (int)Math.Round(Math.Min(rectHeightInBitmap, bitmap.Height - finalY)),
            };
            return rectangle;
        }


        public void Dispose()
        {
            LocalizationManager.ReleaseChangedValuesCallbacks(this);
        }

        public void OnStartClicked(Rectangle rectangle)
        {
            var voc = new VideoOcrConfiguration
            {
                Rectangle = rectangle,
                Interval = _interval,
                VideoPath = _path,
                StableFrameCount = _minFrame,
                ChangeThreshold = _changeThreshold,
                TwoPassOcr = _twoPassOcr
            };
            var videoOcrService = _serviceProvider.GetService<IVideoOcrService>();
            var p = new Progress<VideoOcrProgress>(v => Progress = v);
            var task = videoOcrService.Start(voc, p);
            IsProcessing = true;
            task.ContinueWith(t =>
            {
                IsProcessing = false;
                if (t.IsCompletedSuccessfully)
                {
                    MessageBox.Show(LocalizationManager.GetValue("Str.Stages.VideoOcrSuccess"));
                }
                else
                {
                    MessageBox.Show(LocalizationManager.GetValue("Str.Stages.VideoOcrFailed"));
                }
            });
        }
    }
}