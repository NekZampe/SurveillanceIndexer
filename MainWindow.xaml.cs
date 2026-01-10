using System;
using System.Threading.Tasks;
using System.Windows;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using SurveillanceIndexer.Services;

namespace SurveillanceIndexer
{
    public partial class MainWindow : System.Windows.Window
    {
        private VideoProcessor _processor;

        public MainWindow()
        {
            InitializeComponent();

            // 1. Initialize the service
            _processor = new VideoProcessor();

            // 2. Subscribe to the event we'll add to the service
            _processor.FrameProcessed += OnFrameProcessed;

            // 3. Start the video in a background task
            Task.Run(() =>
            {
                try
                {
                    _processor.ProcessVideo(@"Archive\Test_Video_People_Walking.mp4");
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"Error: {ex.Message}"));
                }
            });
        }

        private void OnFrameProcessed(object sender, Mat frame)
        {
            // Convert Mat to WriteableBitmap (high performance conversion)
            // Note: This requires the OpenCvSharp4.WpfExtensions NuGet package
            var bitmap = frame.ToWriteableBitmap();

            // Freeze the bitmap so it can be passed between threads
            bitmap.Freeze();

            // Use Dispatcher to update the Image control on the UI thread
            Dispatcher.BeginInvoke(new Action(() =>
            {
                VideoPlayer.Source = bitmap;
            }));
        }
    }
}