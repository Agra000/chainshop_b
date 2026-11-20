namespace chainshop_b.Model.Dto.Request
{
    public class ReviewRequest
    {
        public Guid OrderItemId { get; set; }
        public short Rating { get; set; }
        public string? Comment { get; set; }
    }

    // rating beberapa barang dalam satu order sekaligus (dipakai halaman Order History)
    public class OrderReviewRequest
    {
        public Guid OrderId { get; set; }
        public List<OrderReviewItemRequest> Items { get; set; } = new();
    }

    public class OrderReviewItemRequest
    {
        public Guid OrderItemId { get; set; }
        public short Rating { get; set; }
        public string? Comment { get; set; }
    }
}
