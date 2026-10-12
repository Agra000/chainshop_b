namespace chainshop_b.Model.Dto.Request
{
    public class ShipOrderRequest
    {
        public Guid DriverId { get; set; }
        public string TrackingNumber { get; set; } = null!;
        public string InvoiceTxHash { get; set; } = null!;
    }
}
