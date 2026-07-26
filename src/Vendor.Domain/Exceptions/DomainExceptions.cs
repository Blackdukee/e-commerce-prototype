namespace Vendor.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    protected DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class CurrencyMismatchException : DomainException
{
    public string ExpectedCurrency { get; }
    public string ActualCurrency { get; }

    public CurrencyMismatchException(string expectedCurrency, string actualCurrency)
        : base($"Cannot perform currency operations between '{expectedCurrency}' and '{actualCurrency}'.")
    {
        ExpectedCurrency = expectedCurrency;
        ActualCurrency = actualCurrency;
    }
}

public class InvalidStateTransitionException : DomainException
{
    public Type AggregateType { get; }
    public object CurrentState { get; }
    public object TargetState { get; }

    public InvalidStateTransitionException(Type aggregateType, object currentState, object targetState)
        : base($"Invalid state transition for {aggregateType.Name} from '{currentState}' to '{targetState}'.")
    {
        AggregateType = aggregateType;
        CurrentState = currentState;
        TargetState = targetState;
    }
}

public class BusinessRuleViolationException : DomainException
{
    public string RuleName { get; }

    public BusinessRuleViolationException(string message, string ruleName = "BusinessRule")
        : base(message)
    {
        RuleName = ruleName;
    }
}
