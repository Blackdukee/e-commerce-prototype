using Vendor.Application.Common.Interfaces;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Security.Resolvers;

public class AwsSsmSecretResolver : ISecretResolver
{
    private readonly EnvSecretResolver _fallback = new();

    public async Task<string> ResolveSecretAsync(SecretReference secretRef, CancellationToken ct = default)
    {
        var envVarName = secretRef.Path.TrimStart('/').Replace('/', '_').ToUpperInvariant();
        var envVal = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrWhiteSpace(envVal)) return envVal;
        return await _fallback.ResolveSecretAsync(secretRef, ct);
    }

    public Task<string> ResolveSecretAsync(string rawReference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawReference) || !rawReference.StartsWith("ref:"))
            return Task.FromResult(rawReference ?? "");
        try
        {
            var secretRef = new SecretReference(rawReference);
            return ResolveSecretAsync(secretRef, ct);
        }
        catch
        {
            return Task.FromResult(rawReference);
        }
    }
}
