namespace Domain.Entities;

public class Employee
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public ApplicationUser User { get; set; } = default!;

    public int BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public ICollection<Service> Services { get; set; } = new List<Service>();
    public ICollection<WorkingHour> WorkingHours { get; set; } = new List<WorkingHour>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}