namespace chainshop_b.Model.Dto.Request
{
    public class UpdateShippingRequest
    {
        public string Status { get; set; } = null!;
        public string? Note { get; set; }
        public string? Location { get; set; }
    }
}
