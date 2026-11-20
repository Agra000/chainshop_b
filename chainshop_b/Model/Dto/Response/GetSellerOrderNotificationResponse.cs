namespace chainshop_b.Model.Dto.Response
{
    // jumlah order yang butuh perhatian seller (badge merah di navbar, dropdown Toko Saya, dan kartu "Orders received")
    public class GetSellerOrderNotificationResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        // status "paid": order baru, seller harus terima / tolak
        public int newOrders { get; set; }
        // status "refund_requested": buyer mengajukan refund (diputuskan admin, seller cukup tahu)
        public int refundRequested { get; set; }
        // status "return_delivered": barang retur sudah sampai, seller harus konfirmasi
        public int returnArrived { get; set; }
        // status "accepted": buyer sudah menerima barang, seller bisa klaim dana
        public int readyToClaim { get; set; }
        public int total { get; set; }
    }
}
