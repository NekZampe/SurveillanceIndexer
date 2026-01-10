using SurveillanceIndexer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SurveillanceIndexer.Models;
using Microsoft.EntityFrameworkCore;

namespace SurveillanceIndexer.Contexts
{
    internal class SurveillanceContext : DbContext
    {
        public DbSet<VideoFile> VideoFiles { get; set; }
        public DbSet<DetectionEvent> Detections { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=surveillance.db");

        // This ensures the database exists and has the right tables
        public void Initialize()
        {
            Database.EnsureCreated();
        }
    }
}
