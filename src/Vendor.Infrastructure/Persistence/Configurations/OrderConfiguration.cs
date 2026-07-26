using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Infrastructure.Persistence.ValueConverters;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, val => new OrderId(val));

        builder.Property(x => x.CustomerId)
            .HasConversion(id => id.Value, val => new CustomerId(val));

        builder.Property(x => x.OrderNumber).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.OrderNumber).IsUnique();

        builder.OwnsOne(x => x.ShippingAddress, a =>
        {
            a.Property(p => p.Street).HasColumnName("ShipStreet").HasMaxLength(256);
            a.Property(p => p.City).HasColumnName("ShipCity").HasMaxLength(128);
            a.Property(p => p.State).HasColumnName("ShipState").HasMaxLength(128);
            a.Property(p => p.ZipCode).HasColumnName("ShipZipCode").HasMaxLength(32);
            a.Property(p => p.CountryCode).HasColumnName("ShipCountryCode").HasMaxLength(8);
        });

        builder.Property(x => x.Subtotal).HasConversion<MoneyConverter>();
        builder.Property(x => x.Tax).HasConversion<MoneyConverter>();
        builder.Property(x => x.ShippingCost).HasConversion<MoneyConverter>();
        builder.Property(x => x.Discount).HasConversion<MoneyConverter>();
        builder.Property(x => x.Total).HasConversion<MoneyConverter>();

        builder.OwnsMany(x => x.Lines, l =>
        {
            l.WithOwner().HasForeignKey(p => p.OrderId);
            l.HasKey(p => p.Id);

            l.Property(p => p.OrderId).HasConversion(id => id.Value, val => new OrderId(val));
            l.Property(p => p.ProductVariantId).HasConversion(id => id.Value, val => new ProductVariantId(val));
            l.Property(p => p.ProductName).IsRequired().HasMaxLength(256);
            l.Property(p => p.Sku).IsRequired().HasMaxLength(128);
            l.Property(p => p.UnitPrice).HasConversion<MoneyConverter>();
        });
    }
}
