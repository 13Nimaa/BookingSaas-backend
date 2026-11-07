using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.Property(b => b.Name).IsRequired().HasMaxLength(150);
        builder.Property(b => b.City).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Address).HasMaxLength(300);
        builder.Property(b => b.TimeZoneId).IsRequired().HasMaxLength(100).HasDefaultValue("Asia/Tehran");

        builder.OwnsMany(b => b.BusinessGallery, gallery =>
        {
            gallery.ToJson();
            gallery.Property(g => g.Url).IsRequired();
            gallery.Property(g => g.PublicId).IsRequired();
        });

        builder.HasOne(b => b.Owner)
            .WithOne(u => u.OwnedBusiness)
            .HasForeignKey<Business>(b => b.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.City); // برای جستجوی روز ۱۳
    }
}