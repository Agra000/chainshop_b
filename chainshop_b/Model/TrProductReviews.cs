//using System.ComponentModel.DataAnnotations;
//using System.ComponentModel.DataAnnotations.Schema;

//namespace chainshop_b.Model
//{
//    [Table("TrProductReviews")]
//    public class TrProductReviews
//    {
//        [Key]
//        [Column("id")]
//        public Guid Id { get; set; } = Guid.NewGuid();

//        [Required]
//        [Column("order_item_id")]
//        public Guid OrderItemId { get; set; }

//        [ForeignKey(nameof(OrderItemId))]
//        public TrOrderItems? OrderItem { get; set; }

//        [Required]
//        [Column("product_id")]
//        public Guid ProductId { get; set; }

//        [ForeignKey(nameof(ProductId))]
//        public TrProducts? Product { get; set; }

//        [Required]
//        [Column("buyer_id")]
//        public Guid BuyerId { get; set; }

//        [ForeignKey(nameof(BuyerId))]
//        public MsUsers? Buyer { get; set; }

//        [Required]
//        [Column("rating")]
//        public short Rating { get; set; }

//        [Column("comment")]
//        public string? Comment { get; set; }

//        [Required]
//        [Column("created_at")]
//        public DateTime CreatedAt { get; set; }
//    }
//}
