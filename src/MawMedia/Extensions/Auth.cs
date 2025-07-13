using MawMedia.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

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
            .AddAuthentication(opts =>
            {
                opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(opts =>
            {
                opts.Authority = authority;
                opts.Audience = audience;
            })
            .Services
            .AddAuthorization(opts =>
            {
                opts.AddPolicy(
                    AuthPolicies.User, p => p
                        .RequireAuthenticatedUser()
                    );

                opts.AddPolicy(
                    AuthPolicies.Admin, p => p
                        .RequireAuthenticatedUser()
                        .RequireRole("Administrator")
                    );

                opts.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new MediaStaticAssetRequirement())
                    .Build();
            });

        return services;
    }
}
