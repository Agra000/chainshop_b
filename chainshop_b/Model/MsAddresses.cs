using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("msaddresses")]
    public class MsAddresses
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
        [MaxLength(60)]
        [Column("label")]
        public string Label { get; set; } = "Home";

        [Required]
        [MaxLength(120)]
        [Column("recipient_name")]
        public string RecipientName { get; set; } = null!;

        [Required]
        [MaxLength(30)]
        [Column("phone")]
        public string Phone { get; set; } = null!;

        [Required]
        [Column("full_address")]
        public string FullAddress { get; set; } = null!;

        [Required]
        [MaxLength(80)]
        [Column("city")]
        public string City { get; set; } = null!;

        [Required]
        [MaxLength(80)]
        [Column("province")]
        public string Province { get; set; } = null!;

        [MaxLength(10)]
        [Column("postal_code")]
        public string? PostalCode { get; set; }

        [Required]
        [Column("is_default")]
        public bool IsDefault { get; set; } = false;

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
