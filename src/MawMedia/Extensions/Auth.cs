using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using MawMedia.Authorization;
using MawMedia.Authorization.Claims;
using MawMedia.Services;

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
            .AddAuthorization(opts =>
            {
                opts.AddPolicy(
                    AuthorizationPolicies.User, p => p
                        .RequireAuthenticatedUser()
                    );

                opts.AddPolicy(
                    AuthorizationPolicies.MediaReader, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope($"{audience}/media:read")
                    );

                opts.AddPolicy(
                    AuthorizationPolicies.MediaWriter, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope($"{audience}/media:write")
                    );

                opts.AddPolicy(
                    AuthorizationPolicies.CommentReader, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope($"{audience}/comments:read")
                    );

                opts.AddPolicy(
                    AuthorizationPolicies.CommentWriter, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope($"{audience}/comments:write")
                    );

                opts.AddPolicy(
                    AuthorizationPolicies.StatsReader, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope($"{audience}/stats:read")
                    );

                opts.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new MediaStaticAssetRequirement())
                    .Build();
            });

        return services;
    }
}
