namespace chainshop_b.Model.Dto.Response
{
    public class GetOrderResponse
    {
        public Guid Id { get; set; }
        public string orderCode { get; set; } = null!;
        public string status { get; set; } = null!;
        public double totalIdr { get; set; }
        public string paymentMethod { get; set; } = null!;
        public string? buyerName { get; set; }
        public string? shopName { get; set; }
        public int itemCount { get; set; }
        public string? firstItemImage { get; set; }
        public string? trackingNumber { get; set; }
        public string createdAt { get; set; } = null!;
        public int refundCount { get; set; }
    }
}
