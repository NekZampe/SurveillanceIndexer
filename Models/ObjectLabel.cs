using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SurveillanceIndexer.Models
{
    [Table("ObjectLabels")]
    public class ObjectLabel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } 

        public virtual ICollection<TrackedEvent> Events { get; set; }
    }
}
