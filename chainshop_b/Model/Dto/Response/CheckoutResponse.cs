namespace chainshop_b.Model.Dto.Response
{
    public class CheckoutResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public long grandTotal { get; set; }
        public List<CheckoutOrderResponse> orders { get; set; } = new();
    }

    public class CheckoutOrderResponse
    {
        public Guid orderId { get; set; }
        public string orderCode { get; set; } = null!;
        public Guid sellerId { get; set; }
        public string shopName { get; set; } = null!;
        // dipakai frontend untuk memanggil smart contract escrow (hold)
        public string sellerWallet { get; set; } = null!;
        public long totalIdr { get; set; }
    }
}
