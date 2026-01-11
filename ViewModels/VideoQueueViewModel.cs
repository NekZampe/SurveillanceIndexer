using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace SurveillanceIndexer.ViewModels
{
    public class VideoQueueViewModel : INotifyPropertyChanged
    {
        // 1. The List Data
        public ObservableCollection<QueueItemViewModel> Items { get; set; } = new ObservableCollection<QueueItemViewModel>();

        // ---------------------------------------------------------
        // METHOD 1: AddVideo (Called by OnDrop)
        // ---------------------------------------------------------
        public void AddVideo(string filePath)
        {
            // Logic: Create a new row item and add it to the list
            Items.Add(new QueueItemViewModel
            {
                FullPath = filePath,
                Status = "Pending"
            });
        }

        // ---------------------------------------------------------
        // METHOD 2: ProcessQueueAsync (Called by Start_Click)
        // ---------------------------------------------------------
        public async Task ProcessQueueAsync()
        {
            foreach (var item in Items)
            {
                if (item.Status == "Completed") continue;

                item.Status = "Processing...";

                // This is where you will eventually call your "VideoProcessor"
                await Task.Delay(1000); // Fake delay for now

                item.Status = "Completed";
            }
        }

        // Boilerplate for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
    }
}