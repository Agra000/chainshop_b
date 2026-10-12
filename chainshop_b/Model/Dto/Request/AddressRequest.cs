namespace chainshop_b.Model.Dto.Request
{
    public class AddressRequest
    {
        public Guid? Id { get; set; }
        public string Label { get; set; } = "Home";
        public string RecipientName { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string FullAddress { get; set; } = null!;
        public string City { get; set; } = null!;
        public string Province { get; set; } = null!;
        public string? PostalCode { get; set; }
        public bool IsDefault { get; set; }
    }
}
