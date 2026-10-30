using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    // pengajuan refund buyer (maksimal 3x per order), diputuskan admin (= arbiter)
    [Table("trrefundrequests")]
    public class TrRefundRequests : BaseModel
    {
        [Required]
        [Column("order_id")]
        public Guid OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public TrOrders? Order { get; set; }

        [Required]
        [Column("buyer_id")]
        public Guid BuyerId { get; set; }

        [ForeignKey(nameof(BuyerId))]
        public MsUsers? Buyer { get; set; }

        // pengajuan ke-berapa (1..3), sama dengan refundCount di smart contract
        [Required]
        [Column("attempt")]
        public int Attempt { get; set; }

        [Required]
        [Column("reason")]
        public string Reason { get; set; } = null!;

        [Column("photo_url")]
        public string? PhotoUrl { get; set; }

        // tx hash invoice onchain yang dimasukkan buyer (bisa dicek di sepolia.etherscan.io)
        [Required]
        [Column("invoice_tx_hash")]
        public string InvoiceTxHash { get; set; } = null!;

        // (lama) tx hash requestRefund di smart contract v1. Di v2 pengajuan refund off-chain, kolom ini kosong.
        [Column("request_tx_hash")]
        public string? RequestTxHash { get; set; }

        // true = barang tidak kunjung sampai (refund tanpa retur)
        [Required]
        [Column("not_delivered")]
        public bool NotDelivered { get; set; }

        // pending, approved, rejected, refunded, expired
        [Required]
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("admin_note")]
        public string? AdminNote { get; set; }

        [Column("decided_by")]
        public Guid? DecidedBy { get; set; }

        [Column("decided_at")]
        public DateTime? DecidedAt { get; set; }

        [Column("decision_tx_hash")]
        public string? DecisionTxHash { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
