namespace chainshop_b.Model.Dto.Request
{
    // v3: buyer menerima barang dengan tanda tangan EIP-712 "Acceptance" (tanpa transaksi / gas)
    public class AcceptOrderRequest
    {
        public string Signature { get; set; } = null!;
        public string? Note { get; set; }
    }

    // v3: seller sudah mengirim releaseWithAcceptance dan menyerahkan tx hash-nya
    public class ClaimPaymentRequest
    {
        public string TxHash { get; set; } = null!;
    }
}
