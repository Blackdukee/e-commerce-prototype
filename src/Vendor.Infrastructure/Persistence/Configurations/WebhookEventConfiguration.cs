using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Entities;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("WebhookEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.EventId)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(x => new { x.Provider, x.EventId })
            .IsUnique();

        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.Property(x => x.ProcessedAtUtc)
            .IsRequired();
    }
}
