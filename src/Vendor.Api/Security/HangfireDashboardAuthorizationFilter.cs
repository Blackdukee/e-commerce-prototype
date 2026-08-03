using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace Vendor.Api.Security;

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize([NotNull] DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var host = httpContext.Request.Host.Host;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole("VendorAdmin");
    }
}
