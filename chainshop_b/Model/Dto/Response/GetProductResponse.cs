namespace chainshop_b.Model.Dto.Response
{
    public class GetProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string CategoryName { get; set; } = null!;
        public long PriceIdr { get; set; }
        public int Stock { get; set; }
        public string? Description { get; set; }
        public string Slug { get; set; } = null!;
        public decimal RatingAvg { get; set; }
        public string SellerName { get; set; } = null!;
    }
}
