namespace Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public string? UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; } = default!;
    public required string Name { get; set; }
    public string? Notes { get; set; }
    public string? PhoneNumber { get; set; }
public int BusinessId {get; set;}
public Business Business { get; set; } = default!;
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}