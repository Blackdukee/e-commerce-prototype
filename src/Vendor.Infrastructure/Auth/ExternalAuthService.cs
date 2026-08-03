using System.Net.Http.Json;
using Google.Apis.Auth;
using Vendor.Application.Interfaces;

namespace Vendor.Infrastructure.Auth;

public class GoogleTokenInfoResponse
{
    public string sub { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public string given_name { get; set; } = string.Empty;
    public string family_name { get; set; } = string.Empty;
}

public class FacebookMeResponse
{
    public string id { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public string first_name { get; set; } = string.Empty;
    public string last_name { get; set; } = string.Empty;
}

public class ExternalAuthService(HttpClient httpClient) : IExternalAuthService
{
    public async Task<ExternalAuthUser?> VerifyGoogleTokenAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;

        try
        {
            // Attempt GoogleJsonWebSignature validation
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken);
            if (payload is not null && !string.IsNullOrEmpty(payload.Subject))
            {
                return new ExternalAuthUser(payload.Subject, payload.Email, payload.GivenName ?? "GoogleUser", payload.FamilyName ?? "User");
            }
        }
        catch
        {
            // Fallback for test / tokeninfo endpoints
        }

        try
        {
            var response = await httpClient.GetFromJsonAsync<GoogleTokenInfoResponse>($"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}", ct);
            if (response == null || string.IsNullOrEmpty(response.sub)) return null;

            return new ExternalAuthUser(response.sub, response.email, response.given_name ?? "GoogleUser", response.family_name ?? "User");
        }
        catch
        {
            return null;
        }
    }

    public async Task<ExternalAuthUser?> VerifyFacebookTokenAsync(string accessToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return null;

        try
        {
            var response = await httpClient.GetFromJsonAsync<FacebookMeResponse>($"https://graph.facebook.com/me?fields=id,email,first_name,last_name&access_token={accessToken}", ct);
            if (response == null || string.IsNullOrEmpty(response.id)) return null;

            return new ExternalAuthUser(response.id, response.email, response.first_name ?? "FBUser", response.last_name ?? "User");
        }
        catch
        {
            return null;
        }
    }
}
