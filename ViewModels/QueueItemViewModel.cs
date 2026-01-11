using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace SurveillanceIndexer.ViewModels
{
    public class QueueItemViewModel : INotifyPropertyChanged
    {
        // 1. RAW DATA: The actual path (We don't need to notify on this if it never changes)
        public string FullPath { get; set; }

        // 2. COMPUTED PROPERTY: What the UI shows (File Name only)
        // We use "=>" so it calculates dynamically from FullPath
        public string FileName => Path.GetFileName(FullPath);

        // 3. NOTIFYING PROPERTY: The Status
        // We MUST trigger OnPropertyChanged here, otherwise the UI stays "Pending" forever.
        private string _status;
        public string Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        // Standard Boilerplate
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}