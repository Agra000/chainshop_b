using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("mssellers")]
    public class MsSellers : BaseModel
    {
        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public MsUsers? User { get; set; }

        [Required]
        [MaxLength(150)]
        [Column("shop_name")]
        public string ShopName { get; set; } = null!;

        [Required]
        [Column("image")]
        public string Image { get; set; } = null!;

        [Column("shop_description")]
        public string? ShopDescription { get; set; }

        [MaxLength(80)]
        [Column("city")]
        public string? City { get; set; }

        [Required]
        [Column("payout_wallet_address")]
        public string PayoutWalletAddress { get; set; } = null!;

        [Required]
        [Column("is_verified")]
        public bool IsVerified { get; set; } = false;

        [Column("rating_avg", TypeName = "numeric(2,1)")]
        public decimal RatingAvg { get; set; } = 0;

        [Required]
        [Column("rating_count")]
        public int RatingCount { get; set; } = 0;
    }
}
