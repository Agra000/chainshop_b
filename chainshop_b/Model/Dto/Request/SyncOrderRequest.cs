namespace chainshop_b.Model.Dto.Request
{
    // tx hash aksi onchain terakhir (opsional), dipakai endpoint /Order/sync
    public class SyncOrderRequest
    {
        public string? TxHash { get; set; }
    }
}
