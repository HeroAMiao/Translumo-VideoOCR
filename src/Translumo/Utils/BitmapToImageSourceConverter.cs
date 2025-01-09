using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Translumo.Utils
{
    [ValueConversion(typeof(Bitmap), typeof(BitmapSource))]
    public class BitmapToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Bitmap bit)
            {
                return null;
            }
            return bit.ToBitmapImage();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}