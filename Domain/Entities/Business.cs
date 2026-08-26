namespace Domain.Entities;

public class Business
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string? Description { get; set; }

    public string OwnerId { get; set; } = default!;
    public ApplicationUser Owner { get; set; } = default!;

    public ICollection<Service> Services { get; set; } = new List<Service>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}