namespace chainshop_b.Model.Dto.Response
{
    public class GetSellerDashboardResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public string? shopName { get; set; }
        public decimal ratingAvg { get; set; }
        public int ratingCount { get; set; }
        public int totalProducts { get; set; }
        public int totalOrders { get; set; }
        public int newOrders { get; set; }
        public int onShippingOrders { get; set; }
        public int completedOrders { get; set; }
        public int totalSold { get; set; }
        // uang yang masih ditahan di escrow (belum di-release)
        public double escrowHeldIdr { get; set; }
        // uang yang sudah diterima seller (sudah di-release)
        public double totalRevenueIdr { get; set; }
        public List<GetProductResponse> products { get; set; } = new();
    }
}
