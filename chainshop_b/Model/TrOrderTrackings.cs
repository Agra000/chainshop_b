using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("trordertrackings")]
    public class TrOrderTrackings : BaseModel
    {
        [Required]
        [Column("order_id")]
        public Guid OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public TrOrders? Order { get; set; }

        [Required]
        [MaxLength(30)]
        [Column("status")]
        public string Status { get; set; } = null!;

        [Column("note")]
        public string? Note { get; set; }

        [MaxLength(120)]
        [Column("location")]
        public string? Location { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
