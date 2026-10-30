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
        public string? phone { get; set; }
        public string? city { get; set; }
        public string? authMethod { get; set; }
        // "user" | "admin"
        public string? role { get; set; }
        public string? lastLoginAt { get; set; }
        public string? joinedAt { get; set; }
        public string? token { get; set; }
        public string? expiresAt { get; set; }
    }
}
