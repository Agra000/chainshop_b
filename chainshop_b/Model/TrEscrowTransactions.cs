using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("trescrowtransactions")]
    public class TrEscrowTransactions : BaseModel
    {
        [Required]
        [Column("order_id")]
        public Guid OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public TrOrders? Order { get; set; }

        // hold (buyer bayar ke escrow), release (escrow ke seller), refund (escrow ke buyer)
        [Required]
        [MaxLength(20)]
        [Column("tx_type")]
        public string TxType { get; set; } = null!;

        [Required]
        [Column("amount_idr")]
        public long AmountIdr { get; set; }

        [Required]
        [Column("tx_hash")]
        public string TxHash { get; set; } = null!;

        [Column("from_wallet")]
        public string? FromWallet { get; set; }

        [Column("to_wallet")]
        public string? ToWallet { get; set; }

        // pending, success, failed
        [Required]
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "success";

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
