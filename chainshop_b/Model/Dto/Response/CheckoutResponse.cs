namespace chainshop_b.Model.Dto.Response
{
    public class CheckoutResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public double grandTotal { get; set; }
        public List<CheckoutOrderResponse> orders { get; set; } = new();
    }

    public class CheckoutOrderResponse
    {
        public Guid orderId { get; set; }
        public string orderCode { get; set; } = null!;
        public Guid sellerId { get; set; }
        public string shopName { get; set; } = null!;
        // dipakai frontend untuk memanggil smart contract escrow (deposit)
        public string sellerWallet { get; set; } = null!;
        public double totalIdr { get; set; }
        public double itemsTotal { get; set; }
        public double shippingFee { get; set; }
        public double gasFeeEstimate { get; set; }
        public int itemCount { get; set; }
        public string onchainOrderId { get; set; } = null!;
        // nilai dalam unit 6 desimal (string supaya aman untuk BigInt di frontend)
        public string itemsTotalUnits { get; set; } = "0";
        public string shippingFeeUnits { get; set; } = "0";
        public string gasFeeUnits { get; set; } = "0";
    }

    // rincian sebelum bayar, dipecah per seller (1 seller = 1 order escrow)
    public class CheckoutPreviewResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public double grandTotal { get; set; }
        public double totalGasFee { get; set; }
        public string? escrowAddress { get; set; }
        public List<CheckoutPreviewGroup> groups { get; set; } = new();
    }

    public class CheckoutPreviewGroup
    {
        public Guid sellerId { get; set; }
        public string shopName { get; set; } = null!;
        public string sellerWallet { get; set; } = null!;
        public int itemCount { get; set; }
        public double itemsTotal { get; set; }
        public double shippingFee { get; set; }
        public double gasFeeEstimate { get; set; }
        public double total { get; set; }
        public List<CheckoutPreviewItem> items { get; set; } = new();
    }

    public class CheckoutPreviewItem
    {
        public Guid productId { get; set; }
        public string name { get; set; } = null!;
        public string? image { get; set; }
        public double price { get; set; }
        public int qty { get; set; }
        public double lineTotal { get; set; }
    }
}
