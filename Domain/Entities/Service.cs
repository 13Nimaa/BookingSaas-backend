namespace Domain.Entities;

public class Service

{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }

    public int BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    // رابطه‌ی چندبه‌چند: هر کارمند چند خدمت می‌تونه بده
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}