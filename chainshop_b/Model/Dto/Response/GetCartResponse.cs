namespace chainshop_b.Model.Dto.Response
{
    public class GetCartResponse
    {
        public Guid Id { get; set; }
        public Guid userId { get; set; }
        public Guid productId { get; set; }
        public int qty { get; set; }
        public long price { get; set; }
        public string slug { get; set; }
    }
}
