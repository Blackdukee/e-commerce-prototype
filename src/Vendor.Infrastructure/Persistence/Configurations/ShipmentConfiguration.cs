using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Shipment;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, val => new ShipmentId(val));

        builder.Property(x => x.OrderId)
            .HasConversion(id => id.Value, val => new OrderId(val));

        builder.Property(x => x.CarrierCode).IsRequired().HasMaxLength(64);

        builder.OwnsOne(x => x.ShippingAddress, a =>
        {
            a.Property(p => p.Street).HasColumnName("ShipStreet").HasMaxLength(256);
            a.Property(p => p.City).HasColumnName("ShipCity").HasMaxLength(128);
            a.Property(p => p.State).HasColumnName("ShipState").HasMaxLength(128);
            a.Property(p => p.ZipCode).HasColumnName("ShipZipCode").HasMaxLength(32);
            a.Property(p => p.CountryCode).HasColumnName("ShipCountryCode").HasMaxLength(8);
        });
    }
}
