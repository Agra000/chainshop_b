namespace chainshop_b.Model.Dto.Response
{
    public class ResultMessageResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public string? url { get; set; }
        public string? idToken { get; set; }
        public string? walletAddress { get; set; }
    }
}
