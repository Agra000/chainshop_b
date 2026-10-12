namespace chainshop_b.Model.Dto.Request
{
    public class ReviewRequest
    {
        public Guid OrderItemId { get; set; }
        public short Rating { get; set; }
        public string? Comment { get; set; }
    }
}
