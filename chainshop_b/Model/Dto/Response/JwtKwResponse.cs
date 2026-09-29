namespace chainshop_b.Model.Dto.Response
{
    public class JwtKwResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public string? email { get; set; }
        public string? username { get; set; }
        public Guid? userId { get; set; }
        public string? walletAddress { get; set; }
        public string? storeId { get; set; }
        public string? storeName { get; set; }
    }
}
