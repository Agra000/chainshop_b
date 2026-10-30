namespace chainshop_b.Model.Dto.Response
{
    // data invoice yang dikirim seller ke smart contract (markShipped)
    public class GetShipmentPayloadResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public string? onchainOrderId { get; set; }
        public string? invoiceId { get; set; }
        public string? courier { get; set; }
        public string? trackingNumber { get; set; }
        public double itemsTotal { get; set; }
        public double shippingFee { get; set; }
        public double gasFeeEstimate { get; set; }
        public double totalPaid { get; set; }
        public List<ShipmentInvoiceItem> items { get; set; } = new();
    }

    public class ShipmentInvoiceItem
    {
        public string name { get; set; } = null!;
        public int qty { get; set; }
        public double unitPrice { get; set; }
        // unit 6 desimal (string untuk BigInt)
        public string unitPriceUnits { get; set; } = "0";
    }
}
