namespace Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public required string Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
