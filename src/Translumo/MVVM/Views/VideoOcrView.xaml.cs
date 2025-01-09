using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Translumo.MVVM.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace Translumo.MVVM.Views
{
    public partial class VideoOcrView : UserControl
    {
        private readonly Binding _bitmapBinding;

        private VideoOcrViewModel ViewModel => DataContext as VideoOcrViewModel;

        public VideoOcrView()
        {
            InitializeComponent();
            // ViewModel.PropertyChanged += ViewModelPropertyChanged;
        }
        //
        // private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        // {
        //     if (e.PropertyName == nameof(VideoOcrViewModel.PreviewBitmap))
        //     {
        //         OnPreviewBitmapChanged();
        //     }
        // }
        //
        // private void OnPreviewBitmapChanged()
        // {
        // }

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
    }
}