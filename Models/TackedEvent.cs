using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SurveillanceIndexer.Models
{
    [Table("TrackedEvents")]
    public class TrackedEvent
    {
        [Key]
        public int Id { get; set; }

        public int VideoFileId { get; set; }
        [ForeignKey("VideoFileId")]
        public virtual VideoFile VideoSource { get; set; }

        public string Label { get; set; }

        public long StartTicks { get; set; }
        public long EndTicks { get; set; }

        // Calculated property
        [NotMapped]
        public TimeSpan Duration => TimeSpan.FromTicks(EndTicks - StartTicks);

        public float MaxConfidence { get; set; }

        public double TotalSeconds => Duration.TotalSeconds;
    }
}
