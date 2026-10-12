namespace chainshop_b.Model.Dto.Response
{
    public class GetProductDetailResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public GetProductResponse? product { get; set; }
        public Guid? sellerId { get; set; }
        public string? sellerCity { get; set; }
        public decimal sellerRating { get; set; }
        public int ratingCount { get; set; }
        public List<GetProductSpecResponse> specifications { get; set; } = new();
    }

    public class GetProductSpecResponse
    {
        public string Key { get; set; } = null!;
        public string Value { get; set; } = null!;
    }
}
