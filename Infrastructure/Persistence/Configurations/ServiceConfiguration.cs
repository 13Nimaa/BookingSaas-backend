using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.Property(s => s.Title).IsRequired().HasMaxLength(150);
        builder.Property(s => s.Price).HasColumnType("decimal(10,2)");

        builder.HasOne(s => s.Business)
            .WithMany(b => b.Services)
            .HasForeignKey(s => s.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        // many-to-many با Employee — EF Core خودش جدول واسط می‌سازه
        builder.HasMany(s => s.Employees)
            .WithMany(e => e.Services)
            .UsingEntity(j => j.ToTable("EmployeeServices"));
    }
}