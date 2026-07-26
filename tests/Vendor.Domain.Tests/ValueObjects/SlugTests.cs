using FluentAssertions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.ValueObjects;

public class SlugTests
{
    [Fact]
    public void Slug_ValidConstruction_NormalizesToLowercase()
    {
        var slug = new Slug("Awesome-Product-Name");

        slug.Value.Should().Be("awesome-product-name");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Slug_NullOrEmpty_ThrowsArgumentException(string invalidSlug)
    {
        Action act = () => _ = new Slug(invalidSlug);

        act.Should().Throw<ArgumentException>();
    }
}
