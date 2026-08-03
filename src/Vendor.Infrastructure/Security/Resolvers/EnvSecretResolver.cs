using Vendor.Application.Common.Interfaces;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Security.Resolvers;

public class EnvSecretResolver : ISecretResolver
{
    public Task<string> ResolveSecretAsync(SecretReference secretRef, CancellationToken ct = default)
    {
        var varName = secretRef.Path;
        var envValue = Environment.GetEnvironmentVariable(varName);
        return Task.FromResult(envValue ?? secretRef.RawReference);
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
