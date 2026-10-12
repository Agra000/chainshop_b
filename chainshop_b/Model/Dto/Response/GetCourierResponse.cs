namespace chainshop_b.Model.Dto.Response
{
    public class GetCourierResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public int activeDrivers { get; set; }
    }
}
