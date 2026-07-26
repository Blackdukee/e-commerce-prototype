using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Product;
using Vendor.Infrastructure.Persistence.ValueConverters;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, val => new CartId(val));

        builder.Property(x => x.CustomerId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                val => val.HasValue ? new CustomerId(val.Value) : null);

        builder.OwnsMany(x => x.Items, i =>
        {
            i.WithOwner().HasForeignKey("CartId");
            i.HasKey("Id");

            i.Property(x => x.ProductVariantId)
                .HasConversion(id => id.Value, val => new ProductVariantId(val));

            i.Property(x => x.UnitPrice).HasConversion<MoneyConverter>();
        });
    }
}
