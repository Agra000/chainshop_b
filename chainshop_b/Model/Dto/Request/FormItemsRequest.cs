namespace chainshop_b.Model.Dto.Request
{
    public class FormItemsRequest
    {
        public string Name { get; set; }
        public Guid CategoryId { get; set; }
        public long PriceIdr { get; set; }
        public int Stock { get; set; }
        public string? Description { get; set; }
        public string Slug { get; set; }
    }
}
