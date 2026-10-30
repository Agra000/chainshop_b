namespace chainshop_b.Model.Dto.Request
{
    // pengajuan refund buyer (off-chain, tanpa transaksi blockchain)
    public class RefundRequest
    {
        public string Reason { get; set; } = null!;
        public string? PhotoUrl { get; set; }
        // tx hash invoice onchain milik order ini (dicek di sepolia.etherscan.io)
        public string InvoiceTxHash { get; set; } = null!;
    }

    public class RefundCheckRequest
    {
        public string InvoiceTxHash { get; set; } = null!;
    }

    // keputusan admin. TxHash hanya wajib saat approve refund "barang tak sampai"
    // (admin memanggil refund() di smart contract, dana langsung ke buyer). Reject / approve dengan retur = off-chain.
    public class RefundDecisionRequest
    {
        public bool Approve { get; set; }
        public string? Note { get; set; }
        public string? TxHash { get; set; }
    }
}
