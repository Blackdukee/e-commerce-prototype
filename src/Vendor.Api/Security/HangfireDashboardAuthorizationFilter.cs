using System.Net;
using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace Vendor.Api.Security;

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize([NotNull] DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var remoteIp = httpContext.Connection.RemoteIpAddress;

        if (remoteIp != null && IPAddress.IsLoopback(remoteIp))
        {
            return true;
        }

        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole("VendorAdmin");
    }
}
