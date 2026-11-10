using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class BusinessFavoriteConfiguration : IEntityTypeConfiguration<BusinessFavorite>
{
    public void Configure(EntityTypeBuilder<BusinessFavorite> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.UserId).IsRequired().HasMaxLength(450);
        builder.Property(f => f.CreatedAt).IsRequired();

        builder.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Business)
            .WithMany()
            .HasForeignKey(f => f.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => new { f.UserId, f.BusinessId }).IsUnique();
        builder.HasIndex(f => f.UserId);
    }
}
