using FluentAssertions;
using Vendor.Application.Modules.Cart;

namespace Vendor.Application.Tests.Modules;

public class CartCommandIdempotencyTests
{
    [Fact]
    public void CartCommands_IdempotencyKeys_AreValidGuidsAndDeterministic()
    {
        var cartId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var guestCartId = Guid.NewGuid();
        var customerCartId = Guid.NewGuid();

        var removeCartItem = new RemoveCartItemCommand(cartId, variantId);
        var removeCartItemRepeat = new RemoveCartItemCommand(cartId, variantId);
        var applyDiscount = new ApplyCartDiscountCodeCommand(cartId, "SAVE20");
        var removeDiscount = new RemoveCartDiscountCodeCommand(cartId);
        var clearCart = new ClearCartCommand(cartId);
        var mergeCart = new MergeGuestCartCommand(guestCartId, customerCartId);
        var processAbandonment = new ProcessCartAbandonmentCommand(24);

        // Verify valid GUID strings
        Guid.TryParse(removeCartItem.IdempotencyKey, out var removeGuid).Should().BeTrue();
        removeGuid.Should().NotBe(Guid.Empty);
        removeCartItem.IdempotencyKey.Should().Be(removeCartItemRepeat.IdempotencyKey);

        Guid.TryParse(applyDiscount.IdempotencyKey, out var applyGuid).Should().BeTrue();
        applyGuid.Should().NotBe(Guid.Empty);

        Guid.TryParse(removeDiscount.IdempotencyKey, out var removeDiscountGuid).Should().BeTrue();
        removeDiscountGuid.Should().NotBe(Guid.Empty);

        Guid.TryParse(clearCart.IdempotencyKey, out var clearGuid).Should().BeTrue();
        clearGuid.Should().NotBe(Guid.Empty);

        Guid.TryParse(mergeCart.IdempotencyKey, out var mergeGuid).Should().BeTrue();
        mergeGuid.Should().NotBe(Guid.Empty);

        Guid.TryParse(processAbandonment.IdempotencyKey, out var abandonGuid).Should().BeTrue();
        abandonGuid.Should().NotBe(Guid.Empty);
    }
}
