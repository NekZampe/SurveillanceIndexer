using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SurveillanceIndexer.Models
{

    [Table("Detections")]
    public class DetectionEvent
    {
        [Key]
        public int Id { get; set; }

        // Foreign Key to the VideoFile
        public int VideoFileId { get; set; }

        [ForeignKey("VideoFileId")]
        public virtual VideoFile VideoSource { get; set; }

        public string Label { get; set; }
        public float Confidence { get; set; }
        public long VideoTimestampTicks { get; set; }

        // Bounding Box
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

}
