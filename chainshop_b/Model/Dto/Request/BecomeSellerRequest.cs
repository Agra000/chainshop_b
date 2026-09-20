namespace chainshop_b.Model.Dto.Request
{
    public class BecomeSellerRequest
    {
        public string ShopName { get; set; } = null!;
        public string ShopSlug { get; set; } = null!;
        public string? ShopDescription { get; set; }
        public string? City { get; set; }
        public string PayoutWalletAddress { get; set; } = null!;
    }
}
