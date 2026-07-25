using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using MawMedia.Authorization;
using MawMedia.Authorization.Claims;
using MawMedia.Services;
using MawMedia.Services.Abstractions;

namespace MawMedia.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddCustomAuth(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        var oauth = config.GetSection("OAuth").Get<OAuthConfig>();

        ArgumentNullException.ThrowIfNull(oauth);
        ArgumentException.ThrowIfNullOrWhiteSpace(oauth.Authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(oauth.Audience);

        services
            .AddScoped<IClaimsTransformation, MediaIdentityClaimsTransformation>()
            .AddHttpClient<IUserInfoClient, UserInfoClient>(client =>
            {
                client.BaseAddress = new Uri(oauth.Authority);

            })
            .AddHeaderPropagation()
            .AddStandardResilienceHandler()
            .Services
            .AddAuthentication(opts =>
            {
                opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(opts =>
            {
                opts.Authority = oauth.Authority;
                opts.Audience = oauth.Audience;
                opts.MapInboundClaims = false;
            })
            .Services
            .AddAuthorizationBuilder()
                .AddPolicy(
                    AuthorizationPolicies.User, p => p
                        .RequireAuthenticatedUser()
                )
                .AddPolicy(
                    AuthorizationPolicies.MediaReader, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope(oauth.Qualify(ApiScopes.MediaRead))
                )
                .AddPolicy(
                    AuthorizationPolicies.MediaWriter, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope(oauth.Qualify(ApiScopes.MediaWrite))
                )
                .AddPolicy(
                    AuthorizationPolicies.CommentsReader, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope(oauth.Qualify(ApiScopes.CommentsRead))
                )
                .AddPolicy(
                    AuthorizationPolicies.CommentsWriter, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope(oauth.Qualify(ApiScopes.CommentsWrite))
                )
                .AddPolicy(
                    AuthorizationPolicies.LocationReader, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope(oauth.Qualify(ApiScopes.LocationRead))
                )
                .AddPolicy(
                    AuthorizationPolicies.LocationWriter, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope(oauth.Qualify(ApiScopes.LocationWrite))
                )
                .AddPolicy(
                    AuthorizationPolicies.StatsReader, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope(oauth.Qualify(ApiScopes.StatsRead))
                )
                // evaluated by hand for the static asset branch rather than by the authorization
                // middleware, because assets are served by middleware and never match an endpoint.
                // this was previously the fallback policy, which applied it to *every* endpointless
                // request - including unmatched routes, which then answered 401/403 instead of 404.
                .AddPolicy(
                    AuthorizationPolicies.MediaStaticAsset, p => p
                        .RequireAuthenticatedUser()
                        .AddRequirements(new MediaStaticAssetRequirement())
                );

        return services;
    }
}
