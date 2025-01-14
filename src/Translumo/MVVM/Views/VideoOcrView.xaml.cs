using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Translumo.MVVM.ViewModels;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace Translumo.MVVM.Views
{
    public partial class VideoOcrView : UserControl
    {
        private readonly Binding _bitmapBinding;
        private bool _mouseIsDown;
        private Point _relativeInitPos;

        private VideoOcrViewModel ViewModel => DataContext as VideoOcrViewModel;

        public VideoOcrView()
        {
            InitializeComponent();
        }
        
        
        
        private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VideoOcrViewModel.SelectedArea))
            {
                OnSelectedAreaChanged();
            }
        }
        
        private void OnSelectedAreaChanged()
        {
            var area = ViewModel.SelectedArea;
            if (area.Size.IsEmpty)
            {
                SelectedArea.Visibility = Visibility.Collapsed;
                return;
            }
            SelectedArea.Visibility = Visibility.Visible;
            Canvas.SetTop(SelectedArea, area.Top);
            Canvas.SetLeft(SelectedArea, area.Left);
            SelectedArea.Width = area.Width;
            SelectedArea.Height = area.Height;
        }

        private void Browse_OnClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog(); 
            openFileDialog.Filter = "video files (*.mkv;*.mp4;*.avi)|*.mkv;*.mp4;*.avi|All files (*.*)|*.*";
            openFileDialog.RestoreDirectory = true;
            Console.WriteLine($"ShowDialog");
            var result = openFileDialog.ShowDialog();
            Console.WriteLine($"result {result}");
            if (result == DialogResult.OK)
            {
                Console.WriteLine($"filename {openFileDialog.FileName}");
                ViewModel.Path = openFileDialog.FileName;
            }
        }

        private void Load_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.OnLoadClicked();
        }

        private void RectCanvas_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            _mouseIsDown = true;
            _relativeInitPos = e.GetPosition(RectCanvas);
            RectCanvas.CaptureMouse();
            ViewModel.SelectedArea = new Rect(_relativeInitPos, new Size(0, 0));
            e.Handled = true;
        }

        private void RectCanvas_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_mouseIsDown)
            {
                return;
            }
            var pos = e.GetPosition(RectCanvas);
            var dx = Math.Abs(pos.X - _relativeInitPos.X);
            var dy = Math.Abs(pos.Y - _relativeInitPos.Y);
            var sx = Math.Min(pos.X, _relativeInitPos.X);
            var sy = Math.Min(pos.Y, _relativeInitPos.Y);
            ViewModel.SelectedArea = new Rect(sx, sy, dx, dy);
            e.Handled = true;
        }

        private void RectCanvas_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_mouseIsDown)
            {
                return;
            }
            _mouseIsDown = false;
            RectCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void VideoOcrView_OnLoaded(object sender, RoutedEventArgs e)
        {
            ViewModel.PropertyChanged += ViewModelPropertyChanged;
        }
    }
}