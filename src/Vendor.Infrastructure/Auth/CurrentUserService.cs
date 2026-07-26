using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Vendor.Application.Interfaces;

namespace Vendor.Infrastructure.Auth;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public Guid? CustomerId => Guid.TryParse(UserId, out var id) ? id : null;

    public string VendorId => "acme-store";

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles => User?.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList() ?? [];
}
