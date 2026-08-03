namespace Vendor.Infrastructure.Identity;

public class GoogleOAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public class FacebookOAuthOptions
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
}
