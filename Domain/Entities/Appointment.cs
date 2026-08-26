// Appointment.cs
namespace Domain.Entities;

public class Appointment
{
    public int Id { get; set; }

    public int ServiceId { get; set; }
    public Service Service { get; set; } = default!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
}

public enum AppointmentStatus { Pending, Confirmed, Cancelled, Completed }