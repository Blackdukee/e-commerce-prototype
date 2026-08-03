using FluentAssertions;
using Moq;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Auth;
using Vendor.Domain.Interfaces.Adapters;

namespace Vendor.Application.Tests.Auth;

public class ResetPasswordCommandHandlerTests
{
    private readonly Mock<IIdentityAuthService> _identityAuthMock = new();
    private readonly Mock<INotificationSender> _notificationSenderMock = new();

    [Fact]
    public async Task ForgotPasswordHandle_SendsPasswordResetEmail_AndReturnsSuccess()
    {
        _identityAuthMock
            .Setup(i => i.GeneratePasswordResetTokenAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync("reset_token_abc");

        var handler = new ForgotPasswordCommandHandler(_identityAuthMock.Object, _notificationSenderMock.Object);
        var command = new ForgotPasswordCommand("user@example.com");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _notificationSenderMock.Verify(n => n.SendPasswordResetAsync("user@example.com", "reset_token_abc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordHandle_ValidToken_ResetsPasswordSuccessfully()
    {
        _identityAuthMock
            .Setup(i => i.ResetPasswordAsync("user@example.com", "valid_token", "NewPass123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new ResetPasswordCommandHandler(_identityAuthMock.Object);
        var command = new ResetPasswordCommand("user@example.com", "valid_token", "NewPass123!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordHandle_InvalidToken_ReturnsFailureResult()
    {
        _identityAuthMock
            .Setup(i => i.ResetPasswordAsync("user@example.com", "invalid_token", "NewPass123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new ResetPasswordCommandHandler(_identityAuthMock.Object);
        var command = new ResetPasswordCommand("user@example.com", "invalid_token", "NewPass123!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ResetPasswordFailed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("notanemail")]
    public async Task ForgotPasswordCommandValidator_InvalidEmail_FailsValidation(string invalidEmail)
    {
        var validator = new Vendor.Application.Modules.Auth.Validators.ForgotPasswordCommandValidator();
        var command = new ForgotPasswordCommand(invalidEmail);

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Theory]
    [InlineData("", "token", "NewPass123!")]
    [InlineData("user@example.com", "", "NewPass123!")]
    [InlineData("user@example.com", "token", "short")]
    public async Task ResetPasswordCommandValidator_InvalidInputs_FailsValidation(string email, string token, string newPassword)
    {
        var validator = new Vendor.Application.Modules.Auth.Validators.ResetPasswordCommandValidator();
        var command = new ResetPasswordCommand(email, token, newPassword);

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }
}
