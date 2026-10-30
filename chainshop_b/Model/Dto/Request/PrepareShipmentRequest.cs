namespace chainshop_b.Model.Dto.Request
{
    // seller cukup memilih kurir + driver, nomor resi & invoice id di-generate backend
    public class PrepareShipmentRequest
    {
        public Guid DriverId { get; set; }
    }
}
