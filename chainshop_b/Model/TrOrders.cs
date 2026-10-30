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

        // total yang dibayar buyer ke escrow = items_total + shipping_fee + gas_fee_estimate (mUSDT)
        [Required]
        // float (float8 / double precision) di database
        [Column("total_idr", TypeName = "double precision")]
        public double TotalIdr { get; set; }

        // ===== escrow onchain (mUSDT, 6 desimal) =====

        // bytes32 order id di smart contract = keccak256(order id DB)
        [MaxLength(66)]
        [Column("onchain_order_id")]
        public string? OnchainOrderId { get; set; }

        [Column("items_total", TypeName = "double precision")]
        public double ItemsTotal { get; set; }

        [Column("shipping_fee", TypeName = "double precision")]
        public double ShippingFee { get; set; }

        // estimasi gas (±) untuk transaksi seller, dihitung smart contract dan seluruhnya diberikan ke seller
        [Column("gas_fee_estimate", TypeName = "double precision")]
        public double GasFeeEstimate { get; set; }

        // jumlah baris barang (dipakai rumus estimasi gas & validasi invoice onchain)
        [Column("item_count")]
        public int ItemCount { get; set; }

        [MaxLength(40)]
        [Column("invoice_id")]
        public string? InvoiceId { get; set; }

        [MaxLength(120)]
        [Column("courier_name")]
        public string? CourierName { get; set; }

        // batas seller ship (3 hari sejak deposit), dibaca dari smart contract
        [Column("seller_deadline_at")]
        public DateTime? SellerDeadlineAt { get; set; }

        // batas buyer terima / refund (3 hari sejak delivered, di-pause saat refund)
        [Column("buyer_deadline_at")]
        public DateTime? BuyerDeadlineAt { get; set; }

        // batas buyer kirim retur / seller konfirmasi retur
        [Column("return_deadline_at")]
        public DateTime? ReturnDeadlineAt { get; set; }

        // sisa waktu timer buyer (detik) saat refund diajukan, dipakai lagi jika refund ditolak
        [Column("buyer_remaining_seconds")]
        public int? BuyerRemainingSeconds { get; set; }

        // v3: tanda tangan EIP-712 "Acceptance" buyer (tanpa gas); seller memakainya untuk klaim dana onchain
        [Column("buyer_accept_signature")]
        public string? BuyerAcceptSignature { get; set; }

        [Column("accepted_at")]
        public DateTime? AcceptedAt { get; set; }

        [Column("refund_count")]
        public int RefundCount { get; set; } = 0;

        // status sebelum refund diajukan (dikembalikan jika refund ditolak)
        [MaxLength(30)]
        [Column("status_before_refund")]
        public string? StatusBeforeRefund { get; set; }

        [MaxLength(60)]
        [Column("return_tracking_number")]
        public string? ReturnTrackingNumber { get; set; }

        // pending_payment, paid, processing, shipped, in_transit, delivered, completed, cancelled,
        // refund_requested, return_pending, return_shipped, return_delivered, refunded
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
