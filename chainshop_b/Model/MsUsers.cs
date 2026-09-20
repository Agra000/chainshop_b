using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("msusers")]
    public class MsUsers
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("wallet_address")]
        public string? WalletAddress { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Required]
        [MaxLength(120)]
        [Column("name")]
        public string Name { get; set; } = "ChainShop User";

        [MaxLength(30)]
        [Column("phone")]
        public string? Phone { get; set; }

        [MaxLength(80)]
        [Column("city")]
        public string? City { get; set; }

        [Column("avatar_url")]
        public string? AvatarUrl { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("auth_method")]
        public string AuthMethod { get; set; } = "wallet";

        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
