namespace chainshop_b.Model.Dto.Response
{
    public class GetRefundResponse
    {
        public Guid Id { get; set; }
        public Guid orderId { get; set; }
        public string? orderCode { get; set; }
        public string? orderStatus { get; set; }
        public string? buyerName { get; set; }
        public string? buyerWallet { get; set; }
        public string? shopName { get; set; }
        public string? sellerWallet { get; set; }
        public double totalPaid { get; set; }
        public double refundAmount { get; set; }
        public int attempt { get; set; }
        public string reason { get; set; } = null!;
        public string? photoUrl { get; set; }
        public string invoiceTxHash { get; set; } = null!;
        public string? requestTxHash { get; set; }
        public bool notDelivered { get; set; }
        public string status { get; set; } = null!;
        public string? adminNote { get; set; }
        public string? decisionTxHash { get; set; }
        public string? decidedAt { get; set; }
        public string createdAt { get; set; } = null!;
    }

    public class GetRefundDetailResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public GetRefundResponse? refund { get; set; }
        public GetOrderDetailResponse? order { get; set; }
    }

    public class RefundCheckResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public bool notDelivered { get; set; }
        public int attempt { get; set; }
    }
}
