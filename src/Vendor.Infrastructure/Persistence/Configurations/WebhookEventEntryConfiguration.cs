using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Payment;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class WebhookEventEntryConfiguration : IEntityTypeConfiguration<WebhookEventEntry>
{
    public void Configure(EntityTypeBuilder<WebhookEventEntry> builder)
    {
        builder.ToTable("WebhookEventEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GatewayName)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.EventId)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(x => new { x.GatewayName, x.EventId })
            .IsUnique();

        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.PayloadHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.ReceivedAtUtc)
            .IsRequired();

        builder.Property(x => x.IsProcessed)
            .IsRequired();
    }
}
