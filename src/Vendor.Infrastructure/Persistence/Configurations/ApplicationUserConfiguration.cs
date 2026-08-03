using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vendor.Infrastructure.Identity;

namespace Vendor.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("AspNetUsers");

        builder.Property(u => u.CustomerId)
            .IsRequired();

        builder.HasIndex(u => u.CustomerId)
            .IsUnique();
    }
}
