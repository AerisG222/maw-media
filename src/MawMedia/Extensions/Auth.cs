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
        var authority = config["OAuth:Authority"];
        var audience = config["OAuth:Audience"];

        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        services
            .AddScoped<IClaimsTransformation, MediaIdentityClaimsTransformation>()
            .AddHttpClient<IUserInfoClient, UserInfoClient>(client =>
            {
                client.BaseAddress = new Uri(authority);

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
                opts.Authority = authority;
                opts.Audience = audience;
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
                        .RequireScope($"{audience}/media:read")
                )
                .AddPolicy(
                    AuthorizationPolicies.MediaWriter, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope($"{audience}/media:write")
                )
                .AddPolicy(
                    AuthorizationPolicies.CommentReader, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope($"{audience}/comments:read")
                )
                .AddPolicy(
                    AuthorizationPolicies.CommentWriter, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope($"{audience}/comments:write")
                )
                .AddPolicy(
                    AuthorizationPolicies.LocationReader, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope($"{audience}/location:read")
                )
                .AddPolicy(
                    AuthorizationPolicies.LocationWriter, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope($"{audience}/location:write")
                )
                .AddPolicy(
                    AuthorizationPolicies.StatsReader, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope($"{audience}/stats:read")
                )
                .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new MediaStaticAssetRequirement())
                    .Build());

        return services;
    }
}
