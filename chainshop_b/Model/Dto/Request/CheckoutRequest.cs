namespace chainshop_b.Model.Dto.Request
{
    public class CheckoutRequest
    {
        public Guid AddressId { get; set; }
        public string PaymentMethod { get; set; } = "crypto";
    }
}
