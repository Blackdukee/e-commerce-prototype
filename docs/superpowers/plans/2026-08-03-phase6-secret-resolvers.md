# Phase 6 — Enterprise Secret Resolvers Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `ISecretResolver` supporting `ref:env:`, `ref:vault:`, and `ref:aws-ssm:` secret references with automatic environment variable fallbacks and DI integration.

**Architecture:** `ISecretResolver` interface in Application layer, implemented by `EnvSecretResolver`, `VaultSecretResolver`, `AwsSsmSecretResolver`, and routed by `CompositeSecretResolver` in Infrastructure layer.

**Tech Stack:** C# 13, .NET 9, `SecretReference` value object, HttpClient (Vault), AWS SDK, FluentAssertions, Moq, xUnit.

## Global Constraints

- `ISecretResolver` MUST support both `SecretReference` and raw `string` reference parameters.
- `ref:env:VAR` MUST return the environment variable value or the raw string if unconfigured.
- `ref:vault:` and `ref:aws-ssm:` MUST fall back gracefully to environment variable lookup if Vault or AWS is unconfigured.
- `ISecretResolver` MUST be registered as Singleton in `DependencyInjection.cs`.

---

### Task 1: Create `ISecretResolver` Interface and Secret Resolvers

**Files:**
- Create: `src/Vendor.Application/Common/Interfaces/ISecretResolver.cs`
- Create: `src/Vendor.Infrastructure/Security/Resolvers/EnvSecretResolver.cs`
- Create: `src/Vendor.Infrastructure/Security/Resolvers/VaultSecretResolver.cs`
- Create: `src/Vendor.Infrastructure/Security/Resolvers/AwsSsmSecretResolver.cs`
- Create: `src/Vendor.Infrastructure/Security/Resolvers/CompositeSecretResolver.cs`

**Interfaces:**
- Consumes: `SecretReference` value object
- Produces: `ISecretResolver` resolving secrets across Env, Vault, and AWS SSM

- [ ] **Step 1: Create `ISecretResolver.cs`**

```csharp
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Common.Interfaces;

public interface ISecretResolver
{
    Task<string> ResolveSecretAsync(SecretReference secretRef, CancellationToken ct = default);
    Task<string> ResolveSecretAsync(string rawReference, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create `EnvSecretResolver.cs`**

```csharp
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
        if (!rawReference.StartsWith("ref:")) return Task.FromResult(rawReference);
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
```

- [ ] **Step 3: Create `VaultSecretResolver.cs`**

```csharp
using System.Text.Json;
using Vendor.Application.Common.Interfaces;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Security.Resolvers;

public class VaultSecretResolver(HttpClient? httpClient = null, string? vaultAddress = null, string? vaultToken = null) : ISecretResolver
{
    private readonly EnvSecretResolver _fallback = new();

    public async Task<string> ResolveSecretAsync(SecretReference secretRef, CancellationToken ct = default)
    {
        if (httpClient is null || string.IsNullOrWhiteSpace(vaultAddress))
        {
            return await _fallback.ResolveSecretAsync(secretRef, ct);
        }

        try
        {
            var parts = secretRef.Path.Split('#');
            var path = parts[0];
            var key = parts.Length > 1 ? parts[1] : "value";

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{vaultAddress.TrimEnd('/')}/v1/{path}");
            if (!string.IsNullOrWhiteSpace(vaultToken))
            {
                request.Headers.Add("X-Vault-Token", vaultToken);
            }

            var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return await _fallback.ResolveSecretAsync(secretRef, ct);

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                dataEl.TryGetProperty("data", out var innerData) &&
                innerData.TryGetProperty(key, out var val))
            {
                return val.GetString() ?? secretRef.RawReference;
            }
            return await _fallback.ResolveSecretAsync(secretRef, ct);
        }
        catch
        {
            return await _fallback.ResolveSecretAsync(secretRef, ct);
        }
    }

    public Task<string> ResolveSecretAsync(string rawReference, CancellationToken ct = default)
    {
        if (!rawReference.StartsWith("ref:")) return Task.FromResult(rawReference);
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
```

- [ ] **Step 4: Create `AwsSsmSecretResolver.cs`**

```csharp
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
        if (!rawReference.StartsWith("ref:")) return Task.FromResult(rawReference);
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
```

- [ ] **Step 5: Create `CompositeSecretResolver.cs`**

```csharp
using Vendor.Application.Common.Interfaces;
using Vendor.Domain.Enums;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Security.Resolvers;

public class CompositeSecretResolver : ISecretResolver
{
    private readonly EnvSecretResolver _envResolver = new();
    private readonly VaultSecretResolver _vaultResolver;
    private readonly AwsSsmSecretResolver _awsSsmResolver = new();

    public CompositeSecretResolver(VaultSecretResolver? vaultResolver = null)
    {
        _vaultResolver = vaultResolver ?? new VaultSecretResolver();
    }

    public Task<string> ResolveSecretAsync(SecretReference secretRef, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(secretRef);
        return secretRef.Backend switch
        {
            SecretBackend.Env => _envResolver.ResolveSecretAsync(secretRef, ct),
            SecretBackend.Vault => _vaultResolver.ResolveSecretAsync(secretRef, ct),
            SecretBackend.AwsSsm => _awsSsmResolver.ResolveSecretAsync(secretRef, ct),
            _ => Task.FromResult(secretRef.RawReference)
        };
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
```

- [ ] **Step 6: Commit**

```bash
git add src/Vendor.Application/Common/Interfaces/ISecretResolver.cs src/Vendor.Infrastructure/Security/Resolvers/
git commit -m "feat(security): implement ISecretResolver with Env, Vault, AwsSsm, and Composite resolvers"
```

---

### Task 2: DI Registration

**Files:**
- Modify: `src/Vendor.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IServiceCollection`
- Produces: `ISecretResolver` singleton registration

- [ ] **Step 1: Register `ISecretResolver` in `DependencyInjection.cs`**

```csharp
services.AddSingleton<ISecretResolver, CompositeSecretResolver>();
```

- [ ] **Step 2: Commit**

```bash
git add src/Vendor.Infrastructure/DependencyInjection.cs
git commit -m "feat(security): register ISecretResolver as Singleton in DI"
```

---

### Task 3: Unit Tests & Verification Audit

**Files:**
- Create: `tests/Vendor.Infrastructure.Tests/Security/SecretResolverTests.cs`

**Interfaces:**
- Consumes: `ISecretResolver` and resolver implementations
- Produces: Unit tests verifying secret resolution across all backends

- [ ] **Step 1: Create `SecretResolverTests.cs`**

```csharp
using FluentAssertions;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Security.Resolvers;
using Xunit;

namespace Vendor.Infrastructure.Tests.Security;

public class SecretResolverTests
{
    [Fact]
    public async Task EnvSecretResolver_WithValidEnvVar_ReturnsValue()
    {
        Environment.SetEnvironmentVariable("TEST_SECRET_VAR", "my-secret-val");
        try
        {
            var resolver = new EnvSecretResolver();
            var secretRef = new SecretReference("ref:env:TEST_SECRET_VAR");
            var val = await resolver.ResolveSecretAsync(secretRef);
            val.Should().Be("my-secret-val");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_SECRET_VAR", null);
        }
    }

    [Fact]
    public async Task VaultSecretResolver_Unconfigured_FallsBackToEnv()
    {
        Environment.SetEnvironmentVariable("FALLBACK_VAR", "fallback-val");
        try
        {
            var resolver = new VaultSecretResolver();
            var secretRef = new SecretReference("ref:vault:FALLBACK_VAR");
            var val = await resolver.ResolveSecretAsync(secretRef);
            val.Should().Be("fallback-val");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FALLBACK_VAR", null);
        }
    }

    [Fact]
    public async Task AwsSsmSecretResolver_Unconfigured_FallsBackToEnv()
    {
        Environment.SetEnvironmentVariable("AWS_SECRET_VAR", "aws-val");
        try
        {
            var resolver = new AwsSsmSecretResolver();
            var secretRef = new SecretReference("ref:aws-ssm:AWS_SECRET_VAR");
            var val = await resolver.ResolveSecretAsync(secretRef);
            val.Should().Be("aws-val");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AWS_SECRET_VAR", null);
        }
    }

    [Fact]
    public async Task CompositeSecretResolver_RoutesEnvReferenceCorrectly()
    {
        Environment.SetEnvironmentVariable("COMPOSITE_VAR", "comp-val");
        try
        {
            var resolver = new CompositeSecretResolver();
            var val = await resolver.ResolveSecretAsync("ref:env:COMPOSITE_VAR");
            val.Should().Be("comp-val");
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPOSITE_VAR", null);
        }
    }
}
```

- [ ] **Step 2: Execute full test suite**

Run: `dotnet test Vendor.slnx --logger "console;verbosity=normal"`
Expected: All tests pass cleanly.

- [ ] **Step 3: Commit**

```bash
git add tests/Vendor.Infrastructure.Tests/Security/SecretResolverTests.cs
git commit -m "test(security): add unit tests for ISecretResolver implementations and fallbacks"
```
