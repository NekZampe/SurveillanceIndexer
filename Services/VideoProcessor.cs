using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using SurveillanceIndexer.Contexts;
using SurveillanceIndexer.Models;
using Size = OpenCvSharp.Size;
using Point = OpenCvSharp.Point;

namespace SurveillanceIndexer.Services
{
    internal class VideoProcessor : IDisposable
    {
        private Net _dnn;
        private string[] _classLabels;
        private IDbContextFactory<SurveillanceContext> _contextFactory;
        private DatabaseService _databaseService;
        private readonly ConcurrentQueue<TrackedEvent> _eventQueue = new();

        // Store minimal detection info temporarily
        private readonly ConcurrentDictionary<int, int> _detectionCounts = new();

        public event EventHandler<Mat> FrameProcessed;

        private readonly Dictionary<int, TrackedEvent> _activeEvents = new();
        private readonly CentroidTracker _tracker;
        private CancellationTokenSource _cts;
        private int _videoFileId;
        private long _currentTimestampTicks;

        public VideoProcessor(IDbContextFactory<SurveillanceContext> contextFactory, DatabaseService databaseService)
        {
            _contextFactory = contextFactory;
            _databaseService = databaseService;

            // Get path to Assets folder
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
            if (!File.Exists(namesPath))
                throw new FileNotFoundException($"Names file not found: {namesPath}");

            // Load the network
            _dnn = CvDnn.ReadNetFromDarknet(configPath, weightsPath);

            // Set preferable backend and target
            _dnn.SetPreferableBackend(Backend.OPENCV);
            _dnn.SetPreferableTarget(Target.CPU);

            _classLabels = File.ReadAllLines(namesPath);
            _tracker = new CentroidTracker(maxDisappeared: 30);
        }

        public void StartProcessing()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => SaveLoop(_cts.Token));
            Debug.WriteLine("[PROCESSOR] Started.");
        }

        public async Task StopProcessingAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();

                // Finish any active events forcefully (video ended)
                FinishAllActiveEvents(DateTime.Now);

                // Give DB time to save
                await Task.Delay(500);

                _cts.Dispose();
                _cts = null;
            }
        }

        public async Task ProcessVideo(string videoPath)
        {
            using var capture = new VideoCapture(videoPath);
            if (!capture.IsOpened())
            {
                throw new Exception($"Could not open video file at: {videoPath}");
            }

            // Get video metadata
            double fps = capture.Fps;
            int width = (int)capture.FrameWidth;
            int height = (int)capture.FrameHeight;
            int totalFrames = (int)capture.FrameCount;
            double durationSeconds = totalFrames / fps;
            long fileSize = new FileInfo(videoPath).Length;

            // Create or get VideoFile record
            _videoFileId = await GetOrCreateVideoFileAsync(videoPath, fileSize, durationSeconds, width, height, fps);

            long frameNumber = 0;
            using Mat frame = new Mat();

            StartProcessing();

            while (true)
            {
                // Read the next frame from the file
                if (!capture.Read(frame) || frame.Empty())
                    break;

                // Calculate current timestamp in ticks
                _currentTimestampTicks = (long)((frameNumber / fps) * TimeSpan.TicksPerSecond);

                ProcessFrame(frame);

                FrameProcessed?.Invoke(this, frame);

                frameNumber++;
                Cv2.WaitKey(1);
            }

            await StopProcessingAsync();
        }

        private async Task<int> GetOrCreateVideoFileAsync(string videoPath, long fileSize, double durationSeconds, int width, int height, double frameRate)
        {
            using var db = _contextFactory.CreateDbContext();

            string fileName = Path.GetFileName(videoPath);
            string fullPath = Path.GetFullPath(videoPath);

            // Check if this video file already exists in the database
            var existingVideo = await db.VideoFiles
                .FirstOrDefaultAsync(v => v.FullPath == fullPath);

            if (existingVideo != null)
            {
                Debug.WriteLine($"[VIDEO] Found existing video file record: ID={existingVideo.Id}");
                return existingVideo.Id;
            }

            // Create new VideoFile record
            var videoFile = new VideoFile
            {
                FileName = fileName,
                FullPath = fullPath,
                FileSize = fileSize,
                DurationSeconds = durationSeconds,
                Width = width,
                Height = height,
                FrameRate = frameRate,
                IngestedDate = DateTime.Now
            };

            db.VideoFiles.Add(videoFile);
            await db.SaveChangesAsync();

            Debug.WriteLine($"[VIDEO] Created new video file record: ID={videoFile.Id}, File={fileName}");
            return videoFile.Id;
        }

        // YOLOV3-tiny: Box relative to center (first 4 data points)
        // Center X, Center Y, Width, Height
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

                // Track objects and create events
                TrackDetections(boxes, confidences, classIds, indices);

                DrawResults(frame, boxes, confidences, classIds, indices);
            }
            finally
            {
                foreach (var m in outputs) m.Dispose();
            }
        }

        private void TrackDetections(List<Rect> boxes, List<float> confidences, List<int> classIds, int[] indices)
        {
            // Prepare rectangles for tracking (filter by classes of interest)
            var detectedBoxes = new List<RectangleF>();
            var detectedClassIds = new List<int>();
            var detectedConfidences = new List<float>();

            foreach (int i in indices)
            {
                string className = _classLabels[classIds[i]];

                // Only track classes we're interested in
                if (!_databaseService.ClassesOfInterest.Contains(className))
                    continue;

                // Convert OpenCV Rect to System.Drawing.RectangleF
                var box = boxes[i];
                detectedBoxes.Add(new RectangleF(box.X, box.Y, box.Width, box.Height));
                detectedClassIds.Add(classIds[i]);
                detectedConfidences.Add(confidences[i]);
            }

            // Update tracker and get tracked objects (returns Dictionary<int, PointF>)
            var trackedObjects = _tracker.Update(detectedBoxes);

            // Build a mapping from detection index to object ID
            var detectionToObjectId = new Dictionary<int, int>();

            for (int detIdx = 0; detIdx < detectedBoxes.Count; detIdx++)
            {
                var detectedCentroid = new PointF(
                    detectedBoxes[detIdx].X + detectedBoxes[detIdx].Width / 2,
                    detectedBoxes[detIdx].Y + detectedBoxes[detIdx].Height / 2
                );

                // Find which tracked object this detection belongs to
                foreach (var kvp in trackedObjects)
                {
                    int objectId = kvp.Key;
                    PointF trackedCentroid = kvp.Value;

                    // Check if centroids match (within small tolerance)
                    float dx = Math.Abs(detectedCentroid.X - trackedCentroid.X);
                    float dy = Math.Abs(detectedCentroid.Y - trackedCentroid.Y);

                    if (dx < 1 && dy < 1) // Same centroid
                    {
                        detectionToObjectId[detIdx] = objectId;
                        break;
                    }
                }
            }

            // Process each detection
            for (int detIdx = 0; detIdx < detectedBoxes.Count; detIdx++)
            {
                if (!detectionToObjectId.ContainsKey(detIdx))
                    continue;

                int objectId = detectionToObjectId[detIdx];
                int classId = detectedClassIds[detIdx];
                float confidence = detectedConfidences[detIdx];
                RectangleF box = detectedBoxes[detIdx];

                // Get or create tracked event
                if (!_activeEvents.ContainsKey(objectId))
                {
                    // New tracked object - create event
                    int labelId = GetOrCreateLabelId(_classLabels[classId]);

                    var trackedEvent = new TrackedEvent
                    {
                        VideoFileId = _videoFileId,
                        LabelId = labelId,
                        StartTicks = _currentTimestampTicks,
                        EndTicks = _currentTimestampTicks,
                        MaxConfidence = confidence
                    };

                    _activeEvents[objectId] = trackedEvent;
                }
                else
                {
                    // Update existing tracked event
                    var trackedEvent = _activeEvents[objectId];
                    trackedEvent.EndTicks = _currentTimestampTicks;
                    trackedEvent.MaxConfidence = Math.Max(trackedEvent.MaxConfidence, confidence);
                }

                // Just count detections, don't store each one
                _detectionCounts.AddOrUpdate(objectId, 1, (key, count) => count + 1);
            }

            // Check for disappeared objects
            var currentTrackedIds = new HashSet<int>(trackedObjects.Keys);
            var disappearedIds = _activeEvents.Keys.Where(id => !currentTrackedIds.Contains(id)).ToList();

            foreach (var id in disappearedIds)
            {
                // Object has disappeared, finalize the event
                var evt = _activeEvents[id];

                // Clean up detection count
                _detectionCounts.TryRemove(id, out _);

                _eventQueue.Enqueue(evt);
                _activeEvents.Remove(id);
            }
        }

        private int GetOrCreateLabelId(string labelName)
        {
            // Use the DatabaseService's LabelCache
            if (_databaseService.LabelCache.TryGetValue(labelName, out int labelId))
            {
                return labelId;
            }

            Debug.WriteLine($"[WARNING] Label '{labelName}' not found in cache!");
            return -1; // Or throw an exception
        }

        public void SetVideoFileId(int videoFileId)
        {
            _videoFileId = videoFileId;
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

                Rect backgroundRect = new Rect(
                    textOrigin.X,
                    textOrigin.Y - textSize.Height - baseline,
                    textSize.Width,
                    textSize.Height + baseline
                );
                Cv2.Rectangle(frame, backgroundRect, color, -1); // -1 fills the rectangle

                // 5. Draw the text in black (for contrast on the green background)
                Cv2.PutText(frame, label, textOrigin, font, fontScale, new Scalar(0, 0, 0), thickness, LineTypes.AntiAlias);
            }
        }

        private void FinishAllActiveEvents(DateTime endTimestamp)
        {
            foreach (var evt in _activeEvents.Values)
            {
                evt.EndTicks = endTimestamp.Ticks;
                _eventQueue.Enqueue(evt);
            }
            _activeEvents.Clear();
            _detectionCounts.Clear();
        }

        // --- The Background Consumer Loop ---
        private async Task SaveLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool savedData = false;

                // Save Tracked Events (without individual detections)
                var evtBatch = new List<TrackedEvent>();
                while (_eventQueue.TryDequeue(out var e) && evtBatch.Count < 50)
                    evtBatch.Add(e);

                if (evtBatch.Count > 0)
                {
                    try
                    {
                        using var db = _contextFactory.CreateDbContext();
                        db.ChangeTracker.AutoDetectChangesEnabled = false;

                        // Add TrackedEvents only
                        db.TrackedEvents.AddRange(evtBatch);

                        await db.SaveChangesAsync(token);

                        Debug.WriteLine($"[DB] Saved {evtBatch.Count} tracked events.");
                        savedData = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ERROR] Save failed: {ex.Message}");
                    }
                }

                // Sleep if we didn't do anything, otherwise go fast
                if (!savedData)
                    await Task.Delay(100, token);
            }
        }

        public void Dispose()
        {
            _dnn?.Dispose();
            _cts?.Dispose();
        }
    }
}