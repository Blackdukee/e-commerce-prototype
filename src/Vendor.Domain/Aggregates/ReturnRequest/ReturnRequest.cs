using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;

namespace Vendor.Domain.Aggregates.ReturnRequest;

public enum ReturnRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Returned,
    Exchanged
}

public enum ResolutionType
{
    Refund,
    Exchange
}

public class ReturnItem
{
    public Guid OrderLineId { get; private set; }
    public ProductVariantId ProductVariantId { get; private set; }
    public int Quantity { get; private set; }
    public string Reason { get; private set; }

    private ReturnItem()
    {
        Reason = null!;
    }

    public ReturnItem(Guid orderLineId, ProductVariantId productVariantId, int quantity, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (quantity <= 0)
        {
            throw new BusinessRuleViolationException("Return item quantity must be greater than zero.", nameof(ReturnItem));
        }

        OrderLineId = orderLineId;
        ProductVariantId = productVariantId;
        Quantity = quantity;
        Reason = reason.Trim();
    }
}

public class ReturnRequest : AggregateRoot<ReturnRequestId>
{
    private readonly List<ReturnItem> _items = [];

    public OrderId OrderId { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public ReturnRequestStatus Status { get; private set; }
    public ResolutionType RequestedResolution { get; private set; }
    public ProductVariantId? ExchangeVariantId { get; private set; }
    public string? AdminNotes { get; private set; }
    public string? Reason => AdminNotes;
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime CreatedAtUtc => RequestedAtUtc;

    public IReadOnlyCollection<ReturnItem> Items => _items.AsReadOnly();

    private ReturnRequest() : base(default!)
    {
    }

    public ReturnRequest(
        ReturnRequestId id,
        OrderId orderId,
        CustomerId customerId,
        ResolutionType requestedResolution,
        IEnumerable<ReturnItem> items,
        ProductVariantId? exchangeVariantId = null) : base(id)
    {
        ArgumentNullException.ThrowIfNull(items, nameof(items));

        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            throw new BusinessRuleViolationException("Return request must contain at least one item.", nameof(ReturnRequest));
        }

        if (requestedResolution == ResolutionType.Exchange && exchangeVariantId == null)
        {
            throw new BusinessRuleViolationException("Exchange resolution requires specifying an exchange variant ID.", nameof(ReturnRequest));
        }

        OrderId = orderId;
        CustomerId = customerId;
        RequestedResolution = requestedResolution;
        ExchangeVariantId = exchangeVariantId;
        _items = itemList;
        Status = ReturnRequestStatus.Pending;
        RequestedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new ReturnRequestCreatedEvent(Id, OrderId, CustomerId, itemList.Count));
    }

    public ReturnRequest(
        ReturnRequestId id,
        OrderId orderId,
        CustomerId customerId,
        string resolutionOrReason,
        IEnumerable<ReturnItem> items,
        ProductVariantId? exchangeVariantId = null)
        : this(
            id,
            orderId,
            customerId,
            Enum.TryParse<ResolutionType>(resolutionOrReason, true, out var res) ? res : ResolutionType.Refund,
            items,
            exchangeVariantId)
    {
        AdminNotes = resolutionOrReason;
    }

    public void Approve(string? adminNotes = null)
    {
        if (Status != ReturnRequestStatus.Pending)
        {
            throw new InvalidStateTransitionException(typeof(ReturnRequest), Status, ReturnRequestStatus.Approved);
        }

        Status = ReturnRequestStatus.Approved;
        if (!string.IsNullOrWhiteSpace(adminNotes))
        {
            AdminNotes = adminNotes.Trim();
        }

        RaiseDomainEvent(new ReturnRequestApprovedEvent(Id, RequestedResolution, DateTime.UtcNow));
    }

    public void Approve(ResolutionType resolution, string? adminNotes = null)
    {
        RequestedResolution = resolution;
        Approve(adminNotes);
    }

    public void Reject(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (Status != ReturnRequestStatus.Pending)
        {
            throw new InvalidStateTransitionException(typeof(ReturnRequest), Status, ReturnRequestStatus.Rejected);
        }

        Status = ReturnRequestStatus.Rejected;
        AdminNotes = reason.Trim();
    }

    public void CompleteReturn()
    {
        if (Status != ReturnRequestStatus.Approved)
        {
            throw new InvalidStateTransitionException(typeof(ReturnRequest), Status, ReturnRequestStatus.Returned);
        }

        if (RequestedResolution != ResolutionType.Refund)
        {
            throw new BusinessRuleViolationException("Cannot complete return for non-refund resolution.", nameof(ReturnRequest));
        }

        Status = ReturnRequestStatus.Returned;
        RaiseDomainEvent(new ReturnCompletedEvent(Id, OrderId, DateTime.UtcNow));
    }

    public void CompleteExchange(OrderId? replacementOrderId = null)
    {
        if (Status != ReturnRequestStatus.Approved)
        {
            throw new InvalidStateTransitionException(typeof(ReturnRequest), Status, ReturnRequestStatus.Exchanged);
        }

        if (RequestedResolution != ResolutionType.Exchange)
        {
            throw new BusinessRuleViolationException("Cannot complete exchange for non-exchange resolution.", nameof(ReturnRequest));
        }

        Status = ReturnRequestStatus.Exchanged;
        RaiseDomainEvent(new ExchangeCompletedEvent(Id, OrderId, replacementOrderId, DateTime.UtcNow));
    }
}
