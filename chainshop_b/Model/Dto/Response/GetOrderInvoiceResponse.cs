namespace chainshop_b.Model.Dto.Response
{
    public class GetOrderInvoiceResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public string? orderCode { get; set; }
        // isi invoice (json) yang di-hash
        public string? invoicePayload { get; set; }
        // hash yang disimpan ke onchain oleh seller
        public string? invoiceHash { get; set; }
        public string? invoiceTxHash { get; set; }
        public bool? isMatch { get; set; }
    }
}
