using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("trorderitems")]
    public class TrOrderItems : BaseModel
    {
        [Required]
        [Column("order_id")]
        public Guid OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public TrOrders? Order { get; set; }

        [Required]
        [Column("product_id")]
        public Guid ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public TrProducts? Product { get; set; }

        [Required]
        [MaxLength(200)]
        [Column("product_name_snapshot")]
        public string ProductNameSnapshot { get; set; } = null!;

        [Required]
        [Column("product_price_snapshot")]
        public long ProductPriceSnapshot { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Required]
        [Column("line_total_idr")]
        public long LineTotalIdr { get; set; }
    }
}
