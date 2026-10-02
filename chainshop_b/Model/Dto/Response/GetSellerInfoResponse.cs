namespace chainshop_b.Model.Dto.Response
{
    public class GetSellerInfoResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public string? shopName { get; set; }
        public string? shopDescription { get; set; }
        public string? city { get; set; }
        public string? payoutWallet { get; set; }
        public string? registeredAt { get; set; }
        public string? shopImage { get; set; }
    }
}
