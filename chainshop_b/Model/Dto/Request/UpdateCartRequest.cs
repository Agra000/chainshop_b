namespace chainshop_b.Model.Dto.Request
{
    public class UpdateCartRequest
    {
        public Guid CartId { get; set; }
        public int Quantity { get; set; }
        public bool? IsSelected { get; set; }
    }
}
