using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("trorders")]
    public class TrOrders : BaseModel
    {
        [Required]
        [MaxLength(40)]
        [Column("order_code")]
        public string OrderCode { get; set; } = null!;

        [Required]
        [Column("buyer_id")]
        public Guid BuyerId { get; set; }

        [ForeignKey(nameof(BuyerId))]
        public MsUsers? Buyer { get; set; }

        [Required]
        [Column("seller_id")]
        public Guid SellerId { get; set; }

        [ForeignKey(nameof(SellerId))]
        public MsSellers? Seller { get; set; }

        [Column("address_id")]
        public Guid? AddressId { get; set; }

        [ForeignKey(nameof(AddressId))]
        public MsAddresses? Address { get; set; }

        [Column("driver_id")]
        public Guid? DriverId { get; set; }

        [ForeignKey(nameof(DriverId))]
        public TrDrivers? Driver { get; set; }

        [Required]
        // float (float8 / double precision) di database
        [Column("total_idr", TypeName = "double precision")]
        public double TotalIdr { get; set; }

        // pending_payment, paid, processing, shipped, in_transit, delivered, completed, cancelled
        [Required]
        [MaxLength(30)]
        [Column("status")]
        public string Status { get; set; } = "pending_payment";

        [Required]
        [MaxLength(30)]
        [Column("payment_method")]
        public string PaymentMethod { get; set; } = "crypto";

        [MaxLength(60)]
        [Column("tracking_number")]
        public string? TrackingNumber { get; set; }

        // hash invoice barang yang dikirim seller (disimpan juga di onchain)
        [Column("invoice_hash")]
        public string? InvoiceHash { get; set; }

        [Column("invoice_tx_hash")]
        public string? InvoiceTxHash { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("paid_at")]
        public DateTime? PaidAt { get; set; }

        [Column("shipped_at")]
        public DateTime? ShippedAt { get; set; }

        [Column("delivered_at")]
        public DateTime? DeliveredAt { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }
    }
}
