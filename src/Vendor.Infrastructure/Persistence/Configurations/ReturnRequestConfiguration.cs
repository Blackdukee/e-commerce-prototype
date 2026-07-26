using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Aggregates.ReturnRequest;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, val => new ReturnRequestId(val));

        builder.Property(x => x.OrderId)
            .HasConversion(id => id.Value, val => new OrderId(val));

        builder.Property(x => x.CustomerId)
            .HasConversion(id => id.Value, val => new CustomerId(val));

        builder.Property(x => x.ExchangeVariantId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                val => val.HasValue ? new ProductVariantId(val.Value) : null);

        builder.OwnsMany(x => x.Items, i =>
        {
            i.WithOwner().HasForeignKey("ReturnRequestId");
            i.HasKey("Id");

            i.Property(x => x.ProductVariantId)
                .HasConversion(id => id.Value, val => new ProductVariantId(val));
        });
    }
}
