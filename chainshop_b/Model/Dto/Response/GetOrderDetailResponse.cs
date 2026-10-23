namespace chainshop_b.Model.Dto.Response
{
    public class GetOrderDetailResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public Guid? orderId { get; set; }
        public string? orderCode { get; set; }
        public string? orderStatus { get; set; }
        public double totalIdr { get; set; }
        public string? paymentMethod { get; set; }
        public string? buyerName { get; set; }
        public string? buyerWallet { get; set; }
        public Guid? sellerId { get; set; }
        public string? shopName { get; set; }
        public string? sellerWallet { get; set; }
        public GetAddressResponse? address { get; set; }
        public string? trackingNumber { get; set; }
        public string? invoiceHash { get; set; }
        public string? invoiceTxHash { get; set; }
        public GetDriverResponse? driver { get; set; }
        public string? createdAt { get; set; }
        public string? paidAt { get; set; }
        public string? shippedAt { get; set; }
        public string? deliveredAt { get; set; }
        public string? completedAt { get; set; }
        public List<GetOrderItemResponse> items { get; set; } = new();
        public List<GetOrderTrackingResponse> trackings { get; set; } = new();
        public List<GetEscrowTxResponse> escrowTransactions { get; set; } = new();
    }

    public class GetOrderItemResponse
    {
        public Guid Id { get; set; }
        public Guid productId { get; set; }
        public string productName { get; set; } = null!;
        public string? image { get; set; }
        public double price { get; set; }
        public int qty { get; set; }
        public double lineTotal { get; set; }
        public bool isReviewed { get; set; }
    }

    public class GetOrderTrackingResponse
    {
        public string status { get; set; } = null!;
        public string? note { get; set; }
        public string? location { get; set; }
        public string createdAt { get; set; } = null!;
    }

    public class GetEscrowTxResponse
    {
        public string txType { get; set; } = null!;
        public double amountIdr { get; set; }
        public string txHash { get; set; } = null!;
        public string? fromWallet { get; set; }
        public string? toWallet { get; set; }
        public string status { get; set; } = null!;
        public string createdAt { get; set; } = null!;
    }
}
