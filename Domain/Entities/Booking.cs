namespace Domain.Entities;

public class Booking
{
    public int Id { get; set; }

    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Notes { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public enum BookingStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed
}