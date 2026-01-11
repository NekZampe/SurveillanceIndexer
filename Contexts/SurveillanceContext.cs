using SurveillanceIndexer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SurveillanceIndexer.Contexts
{
    public class SurveillanceContext : DbContext
    {
        public DbSet<VideoFile> VideoFiles { get; set; }
        public DbSet<ObjectLabel> ObjectLabels { get; set; }
        public DbSet<TrackedEvent> TrackedEvents { get; set; }

        public SurveillanceContext(DbContextOptions<SurveillanceContext> options)
                    : base(options)
        {
        }
    }
}
