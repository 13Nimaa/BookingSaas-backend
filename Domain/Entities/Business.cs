namespace Domain.Entities;

public class Business
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string TimeZoneId { get; set; } = "Asia/Tehran";
    public string? Description { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? ProfileImagePublicId { get; set; }

    /// <summary>
    /// Optional gallery images (Cloudinary). Empty when none uploaded.
    /// </summary>
    public List<BusinessGalleryImage> BusinessGallery { get; set; } = new();

    public string OwnerId { get; set; } = default!;
    public ApplicationUser Owner { get; set; } = default!;

    public ICollection<Service> Services { get; set; } = new List<Service>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}