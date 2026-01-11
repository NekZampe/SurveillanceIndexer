using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SurveillanceIndexer.ViewModels;

namespace SurveillanceIndexer.Views.Components
{
    public partial class VideoQueueView : UserControl
    {
        // Helper property to access the ViewModel easily
        private VideoQueueViewModel ViewModel => (VideoQueueViewModel)DataContext;

        public VideoQueueView()
        {
            InitializeComponent();

            // CONNECT THE BRAIN: This ensures {Binding Items} works
            this.DataContext = new VideoQueueViewModel();
        }

        // EVENT: User drops files
        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string file in files)
                {
                    // Pass the data to the ViewModel
                    ViewModel.AddVideo(file);
                }
            }
        }

        // EVENT: User clicks "Start"
        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            btn.IsEnabled = false; // Disable button while working

            await ViewModel.ProcessQueueAsync(); // Run the logic

            btn.IsEnabled = true; // Re-enable
        }
    }
}
