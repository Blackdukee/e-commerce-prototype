using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Payment;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class PaymentIdempotencyKeyConfiguration : IEntityTypeConfiguration<PaymentIdempotencyKey>
{
    public void Configure(EntityTypeBuilder<PaymentIdempotencyKey> builder)
    {
        builder.ToTable("PaymentIdempotencyKeys");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.KeyUuid)
            .IsRequired();

        builder.HasIndex(x => x.KeyUuid)
            .IsUnique();

        builder.Property(x => x.RequestHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.ResponseStatusCode)
            .IsRequired(false);

        builder.Property(x => x.ResponseBody)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .IsRequired();
    }
}
