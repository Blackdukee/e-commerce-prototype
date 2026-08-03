# Phase 6 — Enterprise Secret Resolvers Pipeline Design Document

**Feature**: Phase 6 Enterprise Secret Resolvers Pipeline  
**Date**: 2026-08-03  
**Status**: APPROVED  

---

## 1. Overview

Phase 6 establishes an enterprise-grade dynamic secret resolution pipeline for the vendor e-commerce platform. It introduces `ISecretResolver` backing `ref:env:`, `ref:vault:`, and `ref:aws-ssm:` secret reference value objects (`SecretReference`), supporting HashiCorp Vault KV v2, AWS SSM Parameter Store, and environment variables with automatic fallback.

---

## 2. Component Architecture & Design

### 2.1 Interface Definition

File: `src/Vendor.Application/Common/Interfaces/ISecretResolver.cs`

```csharp
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Common.Interfaces;

public interface ISecretResolver
{
    Task<string> ResolveSecretAsync(SecretReference secretRef, CancellationToken ct = default);
    Task<string> ResolveSecretAsync(string rawReference, CancellationToken ct = default);
}
```

---

### 2.2 Backend Resolver Architecture

File: `src/Vendor.Infrastructure/Security/Resolvers/`

1. **`EnvSecretResolver`**:
   - Parses `ref:env:VAR_NAME`.
   - Returns `Environment.GetEnvironmentVariable(path) ?? path`.

2. **`VaultSecretResolver`**:
   - Parses `ref:vault:secret/data/mysecret#key`.
   - Makes HTTP GET request to Vault address (`VAULT_ADDR` or `Vault:Address` config).
   - If Vault server is unconfigured, falls back to `EnvSecretResolver`.

3. **`AwsSsmSecretResolver`**:
   - Parses `ref:aws-ssm:/myvendor/db_password`.
   - Uses `AmazonSimpleSystemsManagementClient` to fetch parameter value.
   - If AWS credentials are missing, falls back to `EnvSecretResolver`.

4. **`CompositeSecretResolver`**:
   - Main `ISecretResolver` router.
   - Evaluates `SecretReference.Backend`:
     - `SecretBackend.Env` -> `EnvSecretResolver`
     - `SecretBackend.Vault` -> `VaultSecretResolver`
     - `SecretBackend.AwsSsm` -> `AwsSsmSecretResolver`

---

### 2.3 Dependency Injection & Configuration

In `DependencyInjection.cs`:
- Register `ISecretResolver` as Singleton:
  ```csharp
  services.AddSingleton<ISecretResolver, CompositeSecretResolver>();
  ```

---

## 3. Verification & Testing Criteria

1. **Unit Testing**:
   - `EnvSecretResolver` resolves environment variables correctly.
   - `VaultSecretResolver` falls back cleanly when Vault is unconfigured.
   - `AwsSsmSecretResolver` falls back cleanly when AWS is unconfigured.
   - `CompositeSecretResolver` routes based on `SecretBackend`.
2. **Solution Integrity**: All unit and integration tests passing (`dotnet test`).
