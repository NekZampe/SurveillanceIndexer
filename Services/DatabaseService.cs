using Microsoft.EntityFrameworkCore;
using SurveillanceIndexer.Contexts;
using SurveillanceIndexer.Models;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace SurveillanceIndexer.Services
{
    public class DatabaseService
    {
        // 1. Store the factory for later use
        private readonly IDbContextFactory<SurveillanceContext> _contextFactory;

        // 2. The Cache for fast ID lookups
        public Dictionary<string, int> LabelCache { get; private set; } = new();

        // 3. The Hardcoded list of classes you actually care about
        public HashSet<string> ClassesOfInterest { get; } = new()
        {
            "person",
            "bicycle",
            "car",
            "motorcycle",
            "bus",
            "truck"
        };

        // 4. Constructor Injection: The Host passes the factory here
        public DatabaseService(IDbContextFactory<SurveillanceContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public void Initialize()
        {
            Debug.WriteLine($"[INFO] Initializing Database...");
            // 5. Create a fresh context using the factory
            using var db = _contextFactory.CreateDbContext();

            // Create DB if missing
            db.Database.EnsureCreated();

            // Seed only the classes we care about
            SeedLabels(db, ClassesOfInterest);
        }

        private void SeedLabels(SurveillanceContext db, HashSet<string> labelsToSeed)
        {

            Debug.WriteLine($"[INFO] Seeding Database...");
            // Fetch existing labels from DB first
            var existingLabels = db.ObjectLabels.ToList();
            bool changesMade = false;

            foreach (string name in labelsToSeed)
            {
                // Check if this label is already in the DB
                var match = existingLabels.FirstOrDefault(l => l.Name == name);

                if (match == null)
                {
                    // It's new, add it
                    var newLabel = new ObjectLabel { Name = name };
                    db.ObjectLabels.Add(newLabel);

                    // Add to local list so we don't try to add it twice in one run
                    existingLabels.Add(newLabel);
                    changesMade = true;
                }
            }

            // Save to SQLite only if we added something
            if (changesMade) db.SaveChanges();

            // Populate the Cache so the rest of the app can use it
            LabelCache = db.ObjectLabels.ToDictionary(l => l.Name, l => l.Id);
        }

  
        public SurveillanceContext CreateContext()
        {
            return _contextFactory.CreateDbContext();
        }
    }
}