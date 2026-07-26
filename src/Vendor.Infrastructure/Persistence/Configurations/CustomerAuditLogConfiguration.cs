using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class CustomerAuditLogConfiguration : IEntityTypeConfiguration<CustomerAuditLog>
{
    public void Configure(EntityTypeBuilder<CustomerAuditLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CustomerId)
            .HasConversion(id => id.Value, val => new CustomerId(val))
            .IsRequired();

        builder.Property(a => a.EventType).IsRequired().HasMaxLength(50);
        builder.Property(a => a.DetailsJson).IsRequired();

        builder.Property(a => a.PerformedByCustomerId)
            .HasConversion(id => id.Value, val => new CustomerId(val))
            .IsRequired();

        builder.Property(a => a.TimestampUtc).IsRequired();

        builder.HasIndex(a => a.CustomerId);
    }
}
