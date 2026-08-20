using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Persistence.ValueConverters;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new ProductId(value));

        builder.Property(p => p.Name).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Slug)
            .HasConversion(s => s.Value, v => new Slug(v))
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.BasePrice).HasConversion<MoneyConverter>();
        builder.Property(p => p.Category).HasMaxLength(128);
        builder.PrimitiveCollection(p => p.Categories);
        builder.PrimitiveCollection(p => p.Tags);
        builder.PrimitiveCollection(p => p.Images);
        builder.HasIndex(p => p.Slug).IsUnique();

        builder.Navigation(p => p.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(p => p.Variants, v =>
        {
            v.WithOwner().HasForeignKey(x => x.ProductId);
            v.HasKey(x => x.Id);
            v.Property(x => x.Id).HasConversion(id => id.Value, val => new ProductVariantId(val));
            v.Property(x => x.ProductId).HasConversion(id => id.Value, val => new ProductId(val));
            v.Property(x => x.Sku).IsRequired().HasMaxLength(128);

            v.Property(x => x.PriceAdjustment).HasConversion<MoneyConverter>();
            v.Property(x => x.Weight).HasConversion<WeightConverter>();
            v.Property(x => x.Dimensions).HasConversion<DimensionsConverter>();

            v.HasIndex(x => x.Sku).IsUnique();
        });
    }
}
