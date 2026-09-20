using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("ltproductimages")]
    public class LtProductImages
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
        [Column("url")]
        public string Url { get; set; } = null!;

        [Required]
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
    }
}
