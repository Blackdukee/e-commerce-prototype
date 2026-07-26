# Research: Core Domain Layer Aggregates

**Feature**: 002-domain-layer-aggregates
**Date**: 2026-07-25

---

## R1: Strongly-Typed IDs — `readonly record struct` vs class

**Decision**: Use `public readonly record struct AggregateId(Guid Value)` for all 11 aggregate IDs.

**Rationale**: In .NET 9, `readonly record struct` is the idiomatic zero-dependency approach. It is stack-allocated (zero heap GC pressure), provides structural value equality (`==`, `Equals`, `GetHashCode`) out-of-the-box, guarantees immutability, and is natively supported by EF Core 9 via a simple `ValueConverter<TId, Guid>`. Full type safety prevents parameter-position swap bugs (e.g., passing `CustomerId` where `OrderId` is expected).

**Alternatives considered**:
- `sealed record class` — Reference type; allows `null`; incurs heap allocation for a primitive Guid wrapper.
- Source generators (StronglyTypedId, Vogen) — Violate the zero-external-NuGet-dependency rule.
- Raw `Guid` primitives — No type safety; easy to accidentally substitute one aggregate's ID for another.

**Implementation notes**:
```csharp
public readonly record struct ProductId(Guid Value)
{
    public static ProductId New()   => new(Guid.NewGuid());
    public static ProductId Empty   => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
```
Each aggregate has its own `XxxId.cs` file in its aggregate folder. EF Core maps via `ValueConverter<ProductId, Guid>` registered in the corresponding `IEntityTypeConfiguration<Product>`.

---

## R2: Domain Event Base and `IDomainEvent` Marker Interface

**Decision**: `IDomainEvent` is a BCL-only marker interface with `Guid EventId` and `DateTime OccurredOnUtc`. `AggregateRoot<TId>` manages a `private readonly List<IDomainEvent> _domainEvents` cleared by the Infrastructure layer after outbox enqueue inside the same `SaveChangesAsync` transaction.

**Rationale**: Fully decouples the domain from MediatR or any messaging library. The Infrastructure `SaveChangesAsync` override inspects `ChangeTracker` for modified aggregate roots, extracts their domain events, serialises them to `OutboxMessages`, then calls `ClearDomainEvents()` — all within a single atomic transaction. `IReadOnlyCollection<IDomainEvent>` prevents external mutation of the event list.

**Alternatives considered**:
- MediatR `INotification` — Introduces external NuGet dependency into Domain; violates FR-001.
- Static `DomainEvents.Raise()` publisher — Global state; complicates unit testing; breaks outbox transaction atomicity.
- Public `List<IDomainEvent>` — Violates aggregate encapsulation.

**Implementation notes**:
```csharp
public interface IDomainEvent
{
    Guid EventId        { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId        { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public abstract class AggregateRoot<TId> : Entity<TId> where TId : struct
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected void RaiseDomainEvent(IDomainEvent e) => _domainEvents.Add(e);
    public void ClearDomainEvents()                  => _domainEvents.Clear();
}
```

---

## R3: `Money` Value Object — Currency-Safe Arithmetic, BCL-Only

**Decision**: `public readonly record struct Money(decimal Amount, string Currency)` with explicit operator overloads (`+`, `-`, `*`, `/`) that throw `CurrencyMismatchException` on cross-currency operations. Amount stored as `decimal`.

**Rationale**: `decimal` is the only IEEE-754–safe type for monetary arithmetic in C#. Operator-level guards ensure that `$100 USD + €50 EUR` throws a domain exception immediately rather than producing corrupted financial data silently. Scalar operators (`Money * decimal`) allow quantity-based and percentage-based calculations while preserving the currency.

**Alternatives considered**:
- `float` / `double` — Binary floating-point rounding errors; unsuitable for financial calculations.
- Generic `Money<TCurrency>` — Over-engineered for runtime multi-currency e-commerce; incompatible with EF Core owned-type column mapping.
- External currency libraries (NMoneys) — Violate zero-NuGet-dependency rule.

**Implementation notes**:
```csharp
public readonly record struct Money
{
    public decimal Amount   { get; }
    public string  Currency { get; }

    public Money(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        Amount   = amount;
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money a, Money b)  { Guard(a, b); return new(a.Amount + b.Amount, a.Currency); }
    public static Money operator -(Money a, Money b)  { Guard(a, b); return new(a.Amount - b.Amount, a.Currency); }
    public static Money operator *(Money m, decimal s) => new(m.Amount * s, m.Currency);
    public static Money operator /(Money m, decimal s) => s == 0
        ? throw new DivideByZeroException() : new(m.Amount / s, m.Currency);

    private static void Guard(Money a, Money b)
    {
        if (a.Currency != b.Currency) throw new CurrencyMismatchException(a.Currency, b.Currency);
    }
}
```
EF Core mapping: `OwnsOne(p => p.UnitPrice, b => { b.Property(m => m.Amount).HasColumnName("UnitPriceAmount"); b.Property(m => m.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3); })`.

---

## R4: Order State Machine — Static Transition Dictionary

**Decision**: `OrderStatus` enum + a `static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> AllowedTransitions` inside the `Order` aggregate. State changes only occur via intention-revealing domain methods (`ConfirmPayment()`, `Ship()`, `Cancel()`, etc.) that call `EnsureCanTransitionTo()` and raise the corresponding domain event.

**Rationale**: Centralises the entire lifecycle in one auditable place. Intention-revealing methods enforce both the transition guard and the event raise in a single call, preventing partial state mutations. Zero external dependencies.

**Alternatives considered**:
- GoF State Pattern (class per state) — High complexity; creates EF Core discriminator hierarchy mapping challenges.
- External state machine libraries (Stateless, Automatonymous) — Violate zero-NuGet-dependency rule for Domain.
- Ad-hoc `if` guards scattered across methods — Error-prone; hard to audit; no single source of truth for valid paths.

**Implementation notes**:
```csharp
private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> AllowedTransitions = new()
{
    [Pending]          = [Confirmed, Cancelled],
    [Confirmed]        = [Processing, Cancelled, RefundRequested],
    [Processing]       = [Shipped,   Cancelled, RefundRequested],
    [Shipped]          = [Delivered, ReturnRequested, ExchangeRequested],
    [Delivered]        = [ReturnRequested, ExchangeRequested],
    [RefundRequested]  = [Refunded, Confirmed, Processing],
    [ReturnRequested]  = [Returned, Delivered],
    [ExchangeRequested]= [Exchanged, Delivered],
    [Refunded] = [], [Returned] = [], [Exchanged] = [], [Cancelled] = []
};

private void EnsureCanTransitionTo(OrderStatus next)
{
    if (!AllowedTransitions[Status].Contains(next))
        throw new InvalidStateTransitionException(typeof(Order), Status, next);
}
```

---

## R5: Cart Abandonment — Pure Domain Predicate

**Decision**: Expose `bool IsAbandoned(DateTime utcNow, TimeSpan timeout)` and `void MarkAbandoned(DateTime utcNow, TimeSpan timeout)` on the `Cart` aggregate. The caller (Application layer background job) provides `utcNow`.

**Rationale**: Prevents the domain from taking a hidden dependency on `DateTime.UtcNow` (a side-effectful call), keeping aggregates 100% deterministic and unit-testable with injected time values. The Infrastructure layer handles *when* to poll for abandoned carts; the Domain specifies *what* abandonment means.

**Alternatives considered**:
- `DateTime.UtcNow` inside the domain predicate — Breaks test repeatability; non-deterministic unit tests.
- Background timer hosted inside Domain project — Violates Clean Architecture; Domain must be compute-only.

**Implementation notes**:
```csharp
public bool IsAbandoned(DateTime utcNow, TimeSpan timeout)
    => Status == CartStatus.Active && (utcNow - LastModifiedUtc) >= timeout;

public void MarkAbandoned(DateTime utcNow, TimeSpan timeout)
{
    if (!IsAbandoned(utcNow, timeout))
        throw new InvalidOperationException("Cart has not met abandonment criteria.");
    Status = CartStatus.Abandoned;
    RaiseDomainEvent(new CartAbandonedEvent(Id, CustomerId, LastModifiedUtc));
}
```
