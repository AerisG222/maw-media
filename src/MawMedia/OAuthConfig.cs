namespace MawMedia;

// OAuth / identity-provider settings, bound from the "OAuth" configuration section.
// AuthorizationUrl and TokenUrl are optional in configuration - when omitted they are
// derived from Authority (see OAuthExtensions.AddCustomOAuthConfig) to match the Auth0
// tenant hosting login.mikeandwan.us.
public class OAuthConfig
{
    public required string Authority { get; set; }
    public required string Audience { get; set; }
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string? ScalarClientId { get; set; }
}
