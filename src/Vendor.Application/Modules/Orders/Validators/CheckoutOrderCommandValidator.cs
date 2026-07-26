using FluentValidation;
using Vendor.Application.Modules.Orders.Dtos;

namespace Vendor.Application.Modules.Orders.Validators;

public class CheckoutOrderCommandValidator : AbstractValidator<CheckoutOrderCommand>
{
    public CheckoutOrderCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty().WithMessage("CartId is required.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("IdempotencyKey is required.");
        RuleFor(x => x.ShippingAddress).NotNull().WithMessage("ShippingAddress is required.");
        RuleFor(x => x.ShippingAddress.Street).NotEmpty().WithMessage("Street is required.");
        RuleFor(x => x.ShippingAddress.City).NotEmpty().WithMessage("City is required.");
        RuleFor(x => x.ShippingAddress.State).NotEmpty().WithMessage("State is required.");
        RuleFor(x => x.ShippingAddress.ZipCode).NotEmpty().WithMessage("ZipCode is required.");
        RuleFor(x => x.ShippingAddress.CountryCode).NotEmpty().WithMessage("CountryCode is required.");
    }
}
