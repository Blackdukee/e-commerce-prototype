using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Application.Modules.Customers.Commands;

public record SuspendCustomerCommand(Guid TargetCustomerId, string Reason) : ICommand<Result>;

public record ReactivateCustomerCommand(Guid TargetCustomerId) : ICommand<Result>;

public record PromoteCustomerCommand(Guid TargetCustomerId) : ICommand<Result>;

public record DemoteCustomerCommand(Guid TargetCustomerId) : ICommand<Result>;
