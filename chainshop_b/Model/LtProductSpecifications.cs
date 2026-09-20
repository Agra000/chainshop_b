using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("ltproductspecifications")]
    public class LtProductSpecifications
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("product_id")]
        public Guid ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public TrProducts? Product { get; set; }

        [Required]
        [MaxLength(80)]
        [Column("spec_key")]
        public string SpecKey { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        [Column("spec_value")]
        public string SpecValue { get; set; } = null!;

        [Required]
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
    }
}
