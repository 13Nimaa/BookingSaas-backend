using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class EmployeeServiceConfiguration : IEntityTypeConfiguration<EmployeeService>
{
    public void Configure(EntityTypeBuilder<EmployeeService> builder)
    {
        builder.ToTable("EmployeeServices");

        builder.HasKey(es => new { es.EmployeesId, es.ServicesId });

        builder.Property(es => es.PriceOverride)
            .HasColumnType("decimal(10,2)");

        builder.HasOne(es => es.Employee)
            .WithMany()
            .HasForeignKey(es => es.EmployeesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(es => es.Service)
            .WithMany()
            .HasForeignKey(es => es.ServicesId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
