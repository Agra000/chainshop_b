namespace chainshop_b.Model.Dto.Response
{
    public class GetReviewResponse
    {
        public Guid Id { get; set; }
        public Guid productId { get; set; }
        public string? buyerName { get; set; }
        public short rating { get; set; }
        public string? comment { get; set; }
        public string createdAt { get; set; } = null!;
    }
}
