using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.AnalyticsEvent;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class AnalyticsEventConfiguration : IEntityTypeConfiguration<AnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<AnalyticsEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, val => new AnalyticsEventId(val));

        builder.Property(x => x.CustomerId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                val => val.HasValue ? new CustomerId(val.Value) : null);

        builder.Property(x => x.EventType).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Payload).IsRequired();
    }
}
