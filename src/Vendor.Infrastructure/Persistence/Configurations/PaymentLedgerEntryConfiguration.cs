using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Infrastructure.Persistence.ValueConverters;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class PaymentLedgerEntryConfiguration : IEntityTypeConfiguration<PaymentLedgerEntry>
{
    public void Configure(EntityTypeBuilder<PaymentLedgerEntry> builder)
    {
        builder.ToTable("PaymentLedgerEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PaymentId)
            .HasConversion(id => id.Value, val => new PaymentId(val))
            .IsRequired();

        builder.HasIndex(x => x.PaymentId);

        builder.HasIndex(x => new { x.PaymentId, x.SequenceNumber })
            .IsUnique();

        builder.Property(x => x.SequenceNumber)
            .IsRequired();

        builder.Property(x => x.EventType)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasConversion<MoneyConverter>()
            .IsRequired();

        builder.Property(x => x.GatewayReferenceId)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(512)
            .IsRequired(false);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();
    }
}
