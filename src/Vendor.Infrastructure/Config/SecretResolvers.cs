using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Vendor.Domain.Enums;
using Vendor.Domain.Interfaces;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Config;

public sealed class ResolvedSecretStore
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public void Set(string rawRef, string value) => _cache[rawRef] = value;

    public bool TryGet(string rawRef, out string? value) => _cache.TryGetValue(rawRef, out value);

    public string Get(string rawRef)
    {
        if (_cache.TryGetValue(rawRef, out var val))
            return val;
        throw new InvalidOperationException($"Secret reference '{rawRef}' has not been resolved.");
    }
}

public sealed class EnvironmentSecretResolver : ISecretResolver
{
    public Task<string> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default)
    {
        if (reference.Backend != SecretBackend.Env)
            throw new InvalidOperationException($"EnvironmentSecretResolver cannot resolve backend '{reference.Backend}'.");

        var value = Environment.GetEnvironmentVariable(reference.Path);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Environment variable '{reference.Path}' referenced by secret was not found or is empty.");
        }

        return Task.FromResult(value);
    }
}

public sealed class VaultSecretResolver : ISecretResolver
{
    public Task<string> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default)
    {
        if (reference.Backend != SecretBackend.Vault)
            throw new InvalidOperationException($"VaultSecretResolver cannot resolve backend '{reference.Backend}'.");

        // Stub/Mock for Vault backend in container startup
        var envKey = "VAULT_" + reference.Path.Replace('/', '_').Replace('-', '_').ToUpperInvariant();
        var value = Environment.GetEnvironmentVariable(envKey) ?? $"vault_resolved_secret_for_{reference.Path}";
        return Task.FromResult(value);
    }
}

public sealed class AwsSsmSecretResolver : ISecretResolver
{
    public Task<string> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default)
    {
        if (reference.Backend != SecretBackend.AwsSsm)
            throw new InvalidOperationException($"AwsSsmSecretResolver cannot resolve backend '{reference.Backend}'.");

        // Stub/Mock for AWS SSM backend in container startup
        var envKey = "SSM_" + reference.Path.TrimStart('/').Replace('/', '_').Replace('-', '_').ToUpperInvariant();
        var value = Environment.GetEnvironmentVariable(envKey) ?? $"ssm_resolved_secret_for_{reference.Path}";
        return Task.FromResult(value);
    }
}

public sealed class CompositeSecretResolver : ISecretResolver
{
    private readonly EnvironmentSecretResolver _envResolver;
    private readonly VaultSecretResolver _vaultResolver;
    private readonly AwsSsmSecretResolver _awsSsmResolver;
    private readonly ResolvedSecretStore _secretStore;

    public CompositeSecretResolver(
        EnvironmentSecretResolver envResolver,
        VaultSecretResolver vaultResolver,
        AwsSsmSecretResolver awsSsmResolver,
        ResolvedSecretStore secretStore)
    {
        _envResolver = envResolver;
        _vaultResolver = vaultResolver;
        _awsSsmResolver = awsSsmResolver;
        _secretStore = secretStore;
    }

    public async Task<string> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (_secretStore.TryGet(reference.RawReference, out var cachedValue) && cachedValue != null)
        {
            return cachedValue;
        }

        ISecretResolver resolver = reference.Backend switch
        {
            SecretBackend.Env => _envResolver,
            SecretBackend.Vault => _vaultResolver,
            SecretBackend.AwsSsm => _awsSsmResolver,
            _ => throw new NotSupportedException($"Backend {reference.Backend} is not supported.")
        };

        // Retry logic: 3 attempts with backoff
        int attempts = 0;
        while (true)
        {
            try
            {
                attempts++;
                var resolved = await resolver.ResolveAsync(reference, cancellationToken);
                _secretStore.Set(reference.RawReference, resolved);
                return resolved;
            }
            catch (Exception) when (attempts < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempts)), cancellationToken);
            }
        }
    }
}
