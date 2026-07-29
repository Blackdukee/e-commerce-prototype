using FluentAssertions;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Payment.Enums;

namespace Vendor.Domain.Tests.Payments;

public class PaymentIdempotencyKeyTests
{
    [Fact]
    public void PaymentIdempotencyKey_Initialization_SetsDefaultsCorrectly()
    {
        var keyUuid = Guid.NewGuid();
        var hash = "SHA256_TEST_HASH";

        var key = new PaymentIdempotencyKey(keyUuid, hash);

        key.KeyUuid.Should().Be(keyUuid);
        key.RequestHash.Should().Be(hash);
        key.Status.Should().Be(IdempotencyStatus.Processing);
        key.ResponseBody.Should().BeNull();
        key.ResponseStatusCode.Should().BeNull();
        key.ExpiresAtUtc.Should().BeAfter(key.CreatedAtUtc);
    }

    [Fact]
    public void PaymentIdempotencyKey_MatchesHash_ValidatesCorrectly()
    {
        var key = new PaymentIdempotencyKey(Guid.NewGuid(), "hash_abc_123");

        key.MatchesHash("hash_abc_123").Should().BeTrue();
        key.MatchesHash("HASH_ABC_123").Should().BeTrue();
        key.MatchesHash("different_hash").Should().BeFalse();
        key.MatchesHash("").Should().BeFalse();
    }

    [Fact]
    public void PaymentIdempotencyKey_MarkCompleted_UpdatesStatusAndCachedResponse()
    {
        var key = new PaymentIdempotencyKey(Guid.NewGuid(), "hash123");
        var responseJson = "{\"status\":\"success\"}";

        key.MarkCompleted(200, responseJson);

        key.Status.Should().Be(IdempotencyStatus.Completed);
        key.ResponseStatusCode.Should().Be(200);
        key.ResponseBody.Should().Be(responseJson);
    }
}
