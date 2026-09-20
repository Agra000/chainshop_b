using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("trcartitems")]
    public class TrCartItems
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public MsUsers? User { get; set; }

        [Required]
        [Column("product_id")]
        public Guid ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public TrProducts? Product { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; } = 1;

        [Required]
        [Column("is_selected")]
        public bool IsSelected { get; set; } = true;

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Required]
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
