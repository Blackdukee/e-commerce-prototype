using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Domain.Aggregates.Promotion;
using Vendor.Infrastructure.Persistence.ValueConverters;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, val => new PromotionId(val));

        builder.Property(x => x.Code).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.DiscountValue).HasPrecision(18, 4);

        builder.Property(x => x.MaxDiscountAmount).HasConversion<NullableMoneyConverter>();
        builder.Property(x => x.MinOrderAmount).HasConversion<NullableMoneyConverter>();

        builder.ComplexProperty(x => x.Validity, v =>
        {
            v.Property(p => p.StartUtc).HasColumnName("ValidFromUtc");
            v.Property(p => p.EndUtc).HasColumnName("ValidToUtc");
        });
    }
}
