using Microsoft.Extensions.Options;

namespace MawMedia.Extensions;

public static class OAuthExtensions
{
    // standard OIDC scopes. `openid` is required for the /userinfo call the claims
    // transformation makes when onboarding a not-yet-known user.
    static readonly Dictionary<string, string> StandardScopes = new()
    {
        ["openid"] = "Sign in",
        ["profile"] = "Basic profile information",
        ["email"] = "Email address"
    };

    public static IServiceCollection AddCustomOAuthConfig(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        services
            .AddOptions<OAuthConfig>()
            .Bind(config.GetSection("OAuth"))
            .PostConfigure(cfg =>
            {
                var root = cfg.Authority?.TrimEnd('/') ?? string.Empty;

                // defaults match the Auth0 tenant hosting login.mikeandwan.us
                if (string.IsNullOrWhiteSpace(cfg.AuthorizationUrl))
                {
                    cfg.AuthorizationUrl = $"{root}/authorize";
                }

                if (string.IsNullOrWhiteSpace(cfg.TokenUrl))
                {
                    cfg.TokenUrl = $"{root}/oauth/token";
                }
            })
            .Validate(cfg => !string.IsNullOrWhiteSpace(cfg.Authority), "OAuth:Authority is required.")
            .Validate(cfg => !string.IsNullOrWhiteSpace(cfg.Audience), "OAuth:Audience is required.")
            .ValidateOnStart();

        return services;
    }

    // Auth0 issues scopes for a custom API prefixed with the API identifier (audience)
    public static string Qualify(this OAuthConfig config, string scope) =>
        ApiScopes.Qualify(config.Audience, scope);

    // the full set of scopes Scalar should request: the standard OIDC scopes plus every
    // audience-qualified API scope, each paired with a human readable description.
    public static Dictionary<string, string> QualifiedScopes(this OAuthConfig config)
    {
        var scopes = new Dictionary<string, string>(StandardScopes);

        foreach (var kvp in ApiScopes.Descriptions)
        {
            scopes[config.Qualify(kvp.Key)] = kvp.Value;
        }

        return scopes;
    }
}
