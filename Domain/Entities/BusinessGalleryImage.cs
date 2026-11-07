namespace Domain.Entities;

/// <summary>
/// One image in a business gallery. Stored as JSON on <see cref="Business"/>.
/// </summary>
public class BusinessGalleryImage
{
    public string Url { get; set; } = default!;
    public string PublicId { get; set; } = default!;
}
