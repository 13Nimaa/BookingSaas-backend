namespace Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public ApplicationUser User { get; set; } = default!;
    public string? PhoneNumber { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}