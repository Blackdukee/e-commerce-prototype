namespace Vendor.Domain.Enums;

public enum CacheProvider
{
    Memory,
    Redis
}

public enum EmailProvider
{
    Mailtrap,
    Smtp
}

public enum TaxStrategy
{
    Flat,
    TaxJar,
    Avalara,
    None
}

public enum TextDirection
{
    Ltr,
    Rtl
}

public enum CaptureMode
{
    Automatic,
    Manual
}

public enum SecretBackend
{
    Env,
    Vault,
    AwsSsm
}
