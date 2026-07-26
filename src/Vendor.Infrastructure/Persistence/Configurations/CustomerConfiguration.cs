using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, val => new CustomerId(val));

        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.Property(c => c.FirstName).IsRequired().HasMaxLength(128);
        builder.Property(c => c.LastName).IsRequired().HasMaxLength(128);

        builder.Property(c => c.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(CustomerRole.Customer);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(CustomerStatus.Active);

        builder.Property(c => c.SuspendedAtUtc).IsRequired(false);
        builder.Property(c => c.SuspensionReason).HasMaxLength(500).IsRequired(false);
        builder.Property(c => c.RoleChangedAtUtc).IsRequired(false);
        builder.Property(c => c.RoleChangedByCustomerId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                val => val.HasValue ? new CustomerId(val.Value) : null)
            .IsRequired(false);

        builder.HasIndex(c => c.Email).IsUnique();

        builder.OwnsMany(c => c.ShippingAddresses, a =>
        {
            a.WithOwner().HasForeignKey("CustomerId");
            a.Property(x => x.Street).HasMaxLength(256);
            a.Property(x => x.City).HasMaxLength(128);
            a.Property(x => x.State).HasMaxLength(128);
            a.Property(x => x.ZipCode).HasMaxLength(32);
            a.Property(x => x.CountryCode).HasMaxLength(8);
        });
    }
}
