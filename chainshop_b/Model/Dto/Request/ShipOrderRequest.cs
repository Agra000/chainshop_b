namespace chainshop_b.Model.Dto.Request
{
    // dikirim setelah seller memanggil markShipped (invoice tercatat onchain)
    public class ShipOrderRequest
    {
        public string InvoiceTxHash { get; set; } = null!;
    }
}
