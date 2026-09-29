using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("mscategories")]
    public class MsCategories : BaseModel
    {
        [Required]
        [Column("slug")]
        public string Slug { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = null!;
    }
}
