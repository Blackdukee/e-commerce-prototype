using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Infrastructure.Persistence.Entities;

namespace Vendor.Infrastructure.Persistence.Configurations;

public sealed class VendorSettingsConfiguration : IEntityTypeConfiguration<VendorSettings>
{
    public void Configure(EntityTypeBuilder<VendorSettings> builder)
    {
        builder.ToTable("VendorSettings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.VendorId)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(x => x.VendorId)
            .IsUnique();

        builder.Property(x => x.RuntimeConfigJson)
            .IsRequired();

        builder.Property(x => x.Version)
            .IsConcurrencyToken();

        builder.Property(x => x.LastModifiedUtc)
            .IsRequired();

        builder.Property(x => x.LastModifiedBy)
            .IsRequired()
            .HasMaxLength(256);
    }
}
