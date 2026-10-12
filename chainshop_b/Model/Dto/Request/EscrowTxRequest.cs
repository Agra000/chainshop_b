namespace chainshop_b.Model.Dto.Request
{
    public class EscrowTxRequest
    {
        public string TxHash { get; set; } = null!;
        public string? WalletAddress { get; set; }
        public string? Note { get; set; }
    }
}
