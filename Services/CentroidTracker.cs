using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing; 

namespace SurveillanceIndexer.Services
{
    public class CentroidTracker
    {
        private int _nextObjectId = 0;
        private readonly int _maxDisappeared;

        // Dictionary to store ID -> Centroid (PointF)
        public Dictionary<int, PointF> Objects { get; private set; } = new();

        // Count of how many consecutive frames an ID has been missing
        public Dictionary<int, int> Disappeared { get; private set; } = new();

        public CentroidTracker(int maxDisappeared = 50)
        {
            _maxDisappeared = maxDisappeared;
        }

        public void Register(PointF centroid)
        {
            Objects.Add(_nextObjectId, centroid);
            Disappeared.Add(_nextObjectId, 0);
            _nextObjectId++;
        }

        public void Deregister(int objectId)
        {
            Objects.Remove(objectId);
            Disappeared.Remove(objectId);
        }

        // The Main Logic: Input is a list of bounding boxes (Rectangles)
        public Dictionary<int, PointF> Update(List<RectangleF> rects)
        {
            // 1. If no detections, mark everyone as "disappeared"
            if (rects.Count == 0)
            {
                foreach (var id in Disappeared.Keys.ToList())
                {
                    Disappeared[id]++;
                    if (Disappeared[id] > _maxDisappeared) Deregister(id);
                }
                return Objects;
            }

            // 2. Calculate centroids for current frame
            var inputCentroids = new List<PointF>();
            foreach (var r in rects)
            {
                inputCentroids.Add(new PointF(r.X + r.Width / 2, r.Y + r.Height / 2));
            }

            // 3. If we are tracking nothing, register everything
            if (Objects.Count == 0)
            {
                foreach (var c in inputCentroids) Register(c);
            }
            else
            {
                var objectIds = Objects.Keys.ToList();
                var objectCentroids = Objects.Values.ToList();

                var usedRows = new HashSet<int>();
                var usedCols = new HashSet<int>();

                foreach (var objectId in objectIds)
                {
                    var existingCentroid = Objects[objectId];
                    double minDist = double.MaxValue;
                    int bestInputIdx = -1;

                    for (int i = 0; i < inputCentroids.Count; i++)
                    {
                        if (usedCols.Contains(i)) continue;

                        double dist = CalculateDistance(existingCentroid, inputCentroids[i]);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            bestInputIdx = i;
                        }
                    }

                    // If we found a match within reasonable distance
                    if (bestInputIdx != -1 && minDist < 100) // 100 pixels threshold
                    {
                        Objects[objectId] = inputCentroids[bestInputIdx]; // Update position
                        Disappeared[objectId] = 0; // Reset counter
                        usedCols.Add(bestInputIdx);
                        usedRows.Add(objectId);
                    }
                    else
                    {
                        Disappeared[objectId]++;
                        if (Disappeared[objectId] > _maxDisappeared) Deregister(objectId);
                    }
                }

                // Register any new inputs that weren't matched
                for (int i = 0; i < inputCentroids.Count; i++)
                {
                    if (!usedCols.Contains(i)) Register(inputCentroids[i]);
                }
            }

            return Objects;
        }

        private double CalculateDistance(PointF p1, PointF p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }
    }
}