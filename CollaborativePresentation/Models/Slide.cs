using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CollaborativePresentation.Models
{
    public class Slide
    {
        public int Id { get; set; }

        [Required]
        public int Order { get; set; }

        public byte[]? SvgData { get; set; }

        public DateTime LastModified { get; set; }

        public virtual Presentation Presentation { get; set; }
        [Required]
        public int PresentationId { get; set; }
    }
}
