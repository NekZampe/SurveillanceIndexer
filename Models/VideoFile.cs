using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SurveillanceIndexer.Models
{
    [Table("VideoFiles")]
    public class VideoFile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; }

        [Required]
        public string FullPath { get; set; }

        public long FileSize { get; set; }
        public double DurationSeconds { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double FrameRate { get; set; }
        public DateTime IngestedDate { get; set; }

        // Relationship: One video has many detections
        public ICollection<DetectionEvent> Detections { get; set; }
    }
}
