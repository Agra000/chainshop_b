namespace chainshop_b.Model.Dto.Request
{
    public class FormItemsRequest
    {
        public string ItemName { get; set; }
        public string Brand { get; set; }
        public string Category { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
        public decimal Price { get; set; }
        public int StockQty { get; set; }
        public string? Description { get; set; }
        public string? ImageURL { get; set; }
        public decimal? Discount { get; set; }
    }
}
