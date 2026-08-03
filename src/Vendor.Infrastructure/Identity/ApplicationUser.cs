using Microsoft.AspNetCore.Identity;

namespace Vendor.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid CustomerId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
