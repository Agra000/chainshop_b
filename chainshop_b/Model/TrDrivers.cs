using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace chainshop_b.Model
{
    [Table("trdrivers")]
    public class TrDrivers : BaseModel
    {
        [Column("user_id")]
        public Guid? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public MsUsers? User { get; set; }

        [Required]
        [Column("courier_company_id")]
        public Guid CourierCompanyId { get; set; }

        [ForeignKey(nameof(CourierCompanyId))]
        public MsCourierCompanies? CourierCompany { get; set; }

        [Required]
        [MaxLength(120)]
        [Column("full_name")]
        public string FullName { get; set; } = null!;

        [Required]
        [MaxLength(30)]
        [Column("phone")]
        public string Phone { get; set; } = null!;

        [MaxLength(50)]
        [Column("vehicle_type")]
        public string? VehicleType { get; set; }

        [MaxLength(20)]
        [Column("plate_number")]
        public string? PlateNumber { get; set; }

        [Required]
        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
