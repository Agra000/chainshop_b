namespace chainshop_b.Model.Dto.Response
{
    public class GetCartResponse
    {
        public Guid Id { get; set; }
        public Guid userId { get; set; }
        public Guid productId { get; set; }
        public int qty { get; set; }
        public long price { get; set; }
        public string image { get; set; }
        public string? productName { get; set; }
        public int stock { get; set; }
        public bool isSelected { get; set; }
        public long lineTotal { get; set; }
        public Guid? sellerId { get; set; }
        public string? shopName { get; set; }
    }
}
