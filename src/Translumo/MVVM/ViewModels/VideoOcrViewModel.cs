using System;
using Translumo.Utils;

namespace Translumo.MVVM.ViewModels
{
    public class VideoOcrViewModel : BindableBase, IDisposable
    {

        public string VideoTimeStamp => "00:00/00:00";

        private string _path = "no path";
        public string Path
        {
            get
            {
                return _path;
            }
            set
            {
                _path = value;
                OnPropertyChanged();
            }
        }


        public void Dispose()
        {
            LocalizationManager.ReleaseChangedValuesCallbacks(this);
        }
    }
}