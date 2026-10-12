namespace chainshop_b.Model.Dto.Response
{
    public class UploadResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public string? url { get; set; }
        public string? path { get; set; }
    }
}
