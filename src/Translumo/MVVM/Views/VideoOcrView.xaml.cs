using System;
using System.Windows;
using System.Windows.Forms;
using Translumo.MVVM.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace Translumo.MVVM.Views
{
    public partial class VideoOcrView : UserControl
    {
        
        private VideoOcrViewModel ViewModel => DataContext as VideoOcrViewModel;

        public VideoOcrView()
        {
            InitializeComponent();
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
    }
}