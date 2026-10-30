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

        // ===== escrow onchain =====
        public string? onchainOrderId { get; set; }
        public string? escrowAddress { get; set; }
        public string? explorerUrl { get; set; }
        public string? invoiceId { get; set; }
        public string? courierName { get; set; }
        public double itemsTotal { get; set; }
        public double shippingFee { get; set; }
        public double gasFeeEstimate { get; set; }
        public int itemCount { get; set; }
        public string? sellerDeadlineAt { get; set; }
        public string? buyerDeadlineAt { get; set; }
        public string? returnDeadlineAt { get; set; }
        // kapan buyer boleh refund jika barang tidak kunjung sampai
        public string? notDeliveredRefundAt { get; set; }
        public int refundCount { get; set; }
        public int maxRefundRequests { get; set; } = 3;
        public string? statusBeforeRefund { get; set; }
        public string? returnTrackingNumber { get; set; }
        // v3: tanda tangan persetujuan buyer (dipakai seller untuk klaim dana onchain)
        public string? buyerAcceptSignature { get; set; }
        public string? acceptedAt { get; set; }
        public List<GetRefundResponse> refunds { get; set; } = new();
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
