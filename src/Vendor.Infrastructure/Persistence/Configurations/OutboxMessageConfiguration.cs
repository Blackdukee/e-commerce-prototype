using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Infrastructure.Outbox;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.OccurredOnUtc)
            .IsRequired();

        builder.Property(m => m.ProcessedOnUtc)
            .IsRequired(false);

        builder.Property(m => m.Error)
            .IsRequired(false);

        builder.Property(m => m.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.Status)
            .IsRequired()
            .HasDefaultValue(OutboxMessageStatus.Pending);

        builder.Ignore(m => m.CreatedAtUtc);
        builder.Ignore(m => m.ProcessedAtUtc);
    }
}
