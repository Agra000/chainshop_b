namespace chainshop_b.Model.Dto.Request
{
    public class RegisterRequest
    {
        public string Email { get; set; } = null!;
        //public string Password { get; set; } = null!;
        //public string ConfirmPassword { get; set; } = null!;
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
    }
}
