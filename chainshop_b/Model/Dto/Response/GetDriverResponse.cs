namespace chainshop_b.Model.Dto.Response
{
    public class GetDriverResponse
    {
        public Guid Id { get; set; }
        public string fullName { get; set; } = null!;
        public string phone { get; set; } = null!;
        public string? vehicleType { get; set; }
        public string? plateNumber { get; set; }
        public Guid courierCompanyId { get; set; }
        public string? courierCompany { get; set; }
    }
}
