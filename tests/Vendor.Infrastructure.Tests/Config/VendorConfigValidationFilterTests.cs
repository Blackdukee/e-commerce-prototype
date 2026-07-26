using FluentAssertions;
using Vendor.Application.Validators;

namespace Vendor.Infrastructure.Tests.Config;

public class VendorConfigValidationFilterTests
{
    [Fact]
    public void VendorConfigValidator_ValidConfig_PassesValidation()
    {
        var validConfig = TestConfigFactory.CreateValidVendorConfig();
        var validator = new VendorConfigValidator();

        var result = validator.Validate(validConfig);

        result.IsValid.Should().BeTrue();
    }
}
