using System.ComponentModel.DataAnnotations;

namespace Vendor.Api.Options;

/// <summary>
/// Strongly-typed JWT configuration bound from appsettings "Jwt" section.
/// Validated at startup — missing or invalid values crash the container before
/// accepting any traffic.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Jwt:SecretKey is required.")]
    [MinLength(32, ErrorMessage = "Jwt:SecretKey must be at least 32 characters.")]
    public string SecretKey { get; init; } = string.Empty;

    [Required(ErrorMessage = "Jwt:Issuer is required.")]
    public string Issuer { get; init; } = string.Empty;

    [Required(ErrorMessage = "Jwt:Audience is required.")]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 1440, ErrorMessage = "Jwt:ExpiryMinutes must be between 1 and 1440.")]
    public int ExpiryMinutes { get; init; } = 60;
}
