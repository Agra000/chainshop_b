namespace chainshop_b.Model.Dto.Response
{
    public class GetProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Category { get; set; } = null!;
        public long Price { get; set; }
        public int Stock { get; set; }
        public int Sold { get; set; }
        public string? Description { get; set; }
        public string Slug { get; set; } = null!;
        public decimal RatingAvg { get; set; }
        public string Seller { get; set; } = null!;
    }
}
