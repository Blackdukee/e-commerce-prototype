using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.Extensions;
using Vendor.Application.Modules.Customers.Commands;
using Vendor.Application.Modules.Customers.Queries;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Api.Endpoints;

public record SuspendCustomerRequest(string Reason);

public static class AdminCustomerEndpoints
{
    public static RouteGroupBuilder MapAdminCustomerEndpoints(this RouteGroupBuilder group)
    {
        var customers = group.MapGroup("/admin/customers")
            .WithTags("Admin Customer Management")
            .RequireAuthorization();

        // 1. List Customers (Paginated & Filterable)
        customers.MapGet("/", async (
            string? email,
            CustomerRole? role,
            CustomerStatus? status,
            DateTime? registeredFrom,
            DateTime? registeredTo,
            int pageIndex,
            int pageSize,
            ISender mediator,
            CancellationToken ct) =>
        {
            var query = new GetAdminCustomersQuery(
                email,
                role,
                status,
                registeredFrom,
                registeredTo,
                pageIndex <= 0 ? 0 : pageIndex,
                pageSize <= 0 ? 20 : pageSize);

            var result = await mediator.Send(query, ct);
            return result.ToHttpResult();
        });

        // 2. Get Customer Profile & Order History
        customers.MapGet("/{id:guid}", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCustomerDetailQuery(id), ct);
            return result.ToHttpResult();
        });

        // 3. Suspend Customer
        customers.MapPost("/{id:guid}/suspend", async (Guid id, SuspendCustomerRequest request, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SuspendCustomerCommand(id, request.Reason), ct);
            return result.ToHttpResult();
        });

        // 4. Reactivate Customer
        customers.MapPost("/{id:guid}/reactivate", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ReactivateCustomerCommand(id), ct);
            return result.ToHttpResult();
        });

        // 5. Promote Customer to Admin (SuperAdmin-only, Auth rate limiting)
        customers.MapPost("/{id:guid}/promote", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new PromoteCustomerCommand(id), ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting("auth-policy");

        // 6. Demote Admin to Customer (SuperAdmin-only, Auth rate limiting)
        customers.MapPost("/{id:guid}/demote", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DemoteCustomerCommand(id), ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting("auth-policy");

        // 7. Get Audit Log (SuperAdmin-only)
        customers.MapGet("/{id:guid}/audit-log", async (Guid id, int pageIndex, int pageSize, ISender mediator, CancellationToken ct) =>
        {
            var query = new GetCustomerAuditLogsQuery(id, pageIndex <= 0 ? 0 : pageIndex, pageSize <= 0 ? 20 : pageSize);
            var result = await mediator.Send(query, ct);
            return result.ToHttpResult();
        });

        return group;
    }
}
