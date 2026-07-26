using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Infrastructure.Persistence.ValueConverters;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, val => new PaymentId(val));

        builder.Property(x => x.OrderId)
            .HasConversion(id => id.Value, val => new OrderId(val));

        builder.Property(x => x.Amount).HasConversion<MoneyConverter>();
        builder.Property(x => x.RefundedAmount).HasConversion<MoneyConverter>();
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
    }
}
