using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace SurveillanceIndexer.Services
{
    internal class VideoProcessor
    {

        private Net _dnn;

        private string[] _classLabels;

        public event EventHandler<Mat> FrameProcessed;

        public VideoProcessor()
        {
            // Get  path to Assets folder
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string assetsPath = Path.Combine(baseDirectory, "Assets");

            // Specify model files
            string configPath = Path.Combine(assetsPath, "yolov3-tiny.cfg");
            string weightsPath = Path.Combine(assetsPath, "yolov3-tiny.weights");
            string namesPath = Path.Combine(assetsPath, "coco.names");

            // Verify files exist
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Config file not found: {configPath}");
            if (!File.Exists(weightsPath))
                throw new FileNotFoundException($"Weights file not found: {weightsPath}");

            // Load the network
            _dnn = CvDnn.ReadNetFromDarknet(configPath, weightsPath);

            // Set preferable backend and target
            _dnn.SetPreferableBackend(Backend.OPENCV);
            _dnn.SetPreferableTarget(Target.CPU);


            _classLabels = System.IO.File.ReadAllLines(namesPath);


        }

        public void ProcessVideo(string videoPath)
        {
            using var capture = new VideoCapture(videoPath);
            if (!capture.IsOpened())
            {
                throw new Exception($"Could not open video file at: {videoPath}");
            }

            using Mat frame = new Mat();
            while (true)
            {
                // Read the next frame from the file
                if (!capture.Read(frame) || frame.Empty())
                    break;

                ProcessFrame(frame);

                FrameProcessed?.Invoke(this, frame);

                
                Cv2.WaitKey(1);
            }
        }

        // YOLOV3-tiny: Box relative to center ( first 4 data points )
        // Center X
        // Center Y
        // Width
        // Height
        private unsafe void ProcessFrame(Mat frame)
        {
            int frameHeight = frame.Rows;
            int frameWidth = frame.Cols;

            List<Rect> boxes = new();
            List<float> confidences = new();
            List<int> classIds = new();

            using var blob = CvDnn.BlobFromImage(frame, 1 / 255.0, new Size(416, 416), new Scalar(), true, false);
            _dnn.SetInput(blob);

            var outputNames = _dnn.GetUnconnectedOutLayersNames();
            Mat[] outputs = outputNames.Select(name => new Mat()).ToArray();

            try
            {
                _dnn.Forward(outputs, outputNames);

                foreach (Mat output in outputs)
                {
                    // Get a pointer to the start of the Mat's data
                    float* dataPtr = (float*)output.Data.ToPointer();
                    int cols = output.Cols;

                    for (int i = 0; i < output.Rows; i++)
                    {
                        // In YOLO, each row looks like: [x, y, w, h, confidence, class0, class1, ...]
                        // We calculate the offset for the current row
                        float* rowPtr = dataPtr + (i * cols);

                        float objectness = rowPtr[4]; // Index 4 is object confidence
                        if (objectness < 0.5) continue;

                        // Find the best class score starting from index 5
                        float maxScore = 0;
                        int classId = 0;

                        for (int j = 5; j < cols; j++)
                        {
                            if (rowPtr[j] > maxScore)
                            {
                                maxScore = rowPtr[j];
                                classId = j - 5;
                            }
                        }

                        // Combine objectness with class score for the final confidence
                        float finalConfidence = maxScore * objectness;

                        if (finalConfidence > 0.5)
                        {
                            float centerX = rowPtr[0] * frameWidth;
                            float centerY = rowPtr[1] * frameHeight;
                            float width = rowPtr[2] * frameWidth;
                            float height = rowPtr[3] * frameHeight;

                            boxes.Add(new Rect(
                                (int)(centerX - width / 2),
                                (int)(centerY - height / 2),
                                (int)width,
                                (int)height
                            ));
                            confidences.Add(finalConfidence);
                            classIds.Add(classId);
                        }
                    }
                }

                // Apply NMS once after all layers are processed
                CvDnn.NMSBoxes(boxes, confidences, 0.5f, 0.4f, out int[] indices);
                DrawResults(frame, boxes, confidences, classIds, indices);
            }
            finally
            {
                foreach (var m in outputs) m.Dispose();
            }
        }

        private void DrawResults(Mat frame, List<Rect> boxes, List<float> confidences, List<int> classIds, int[] indices)
        {
            foreach (int i in indices)
            {
                Rect box = boxes[i];
                string label = $"{_classLabels[classIds[i]]}: {confidences[i]:P0}";

                // 1. Pick a color (Green for all, or you can randomize based on classId)
                Scalar color = new Scalar(0, 255, 0);

                // 2. Draw the main bounding box
                Cv2.Rectangle(frame, box, color, 2);

                // 3. Prepare the label text
                var font = HersheyFonts.HersheySimplex;
                double fontScale = 0.5;
                int thickness = 1;
                Size textSize = Cv2.GetTextSize(label, font, fontScale, thickness, out int baseline);

                // 4. Draw a filled rectangle behind the text for readability
                // We place it slightly above the box
                Point textOrigin = new Point(box.X, box.Y - 5);
                if (textOrigin.Y < 10) textOrigin.Y = box.Y + 20; // Flip inside if too close to top

                Rect backgroundRect = new Rect(textOrigin.X, textOrigin.Y - textSize.Height - baseline, textSize.Width, textSize.Height + baseline);
                Cv2.Rectangle(frame, backgroundRect, color, -1); // -1 fills the rectangle

                // 5. Draw the text in black (for contrast on the green background)
                Cv2.PutText(frame, label, textOrigin, font, fontScale, new Scalar(0, 0, 0), thickness, LineTypes.AntiAlias);
            }
        }
    }
}
