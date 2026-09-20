using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("trproducts")]
    public class TrProducts
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("seller_id")]
        public Guid SellerId { get; set; }

        [ForeignKey(nameof(SellerId))]
        public MsSellers? Seller { get; set; }

        [Required]
        [Column("category_id")]
        public Guid CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public MsCategories? Category { get; set; }

        [Required]
        [MaxLength(200)]
        [Column("name")]
        public string Name { get; set; } = null!;

        [Required]
        [Column("slug")]
        public string Slug { get; set; } = null!;

        [Column("description")]
        public string? Description { get; set; }

        [Required]
        [Column("price_idr")]
        public long PriceIdr { get; set; }

        [Required]
        [Column("stock")]
        public int Stock { get; set; } = 0;

        [Required]
        [Column("sold_count")]
        public int SoldCount { get; set; } = 0;

        [Column("rating_avg", TypeName = "numeric(2,1)")]
        public decimal RatingAvg { get; set; } = 0;

        [Required]
        [Column("rating_count")]
        public int RatingCount { get; set; } = 0;

        [Required]
        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "active";

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Required]
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
