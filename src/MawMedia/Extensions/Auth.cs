using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
                .AddPolicy(
                    AuthorizationPolicies.FaceRecognitionPublisher, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope(oauth.Qualify(ApiScopes.FaceRecognitionPublish))
                )
                .AddPolicy(
                    AuthorizationPolicies.FaceRecognitionReader, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope(oauth.Qualify(ApiScopes.FaceRecognitionRead))
                )
                // evaluated by hand for the static asset branch rather than by the authorization
                // middleware, because assets are served by middleware and never match an endpoint.
                // this was previously the fallback policy, which applied it to *every* endpointless
                // request - including unmatched routes, which then answered 401/403 instead of 404.
                // place covers take media:read and stop there.  no resource
                // requirement, so the per file lookup MediaStaticAsset performs is
                // deliberately not applied - see AuthorizationPolicies.
                .AddPolicy(
                    AuthorizationPolicies.PlaceCoverStaticAsset, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope(oauth.Qualify(ApiScopes.MediaRead))
                )
                .AddPolicy(
                    AuthorizationPolicies.MediaStaticAsset, p => p
                        .RequireAuthenticatedUser()
                        .AddRequirements(new MediaStaticAssetRequirement())
                )
                // face crops keep the scope the api route they replaced required.
                // without it a caller holding only media:read could pull a face
                // crop by guid, and the point of the scope is that the whole face
                // feature can be withdrawn from a client on its own.
                //
                // it carries no resource requirement, unlike MediaStaticAsset:
                // whether the caller may see a *particular* face is settled by
                // StaticFilesExtensions afterwards, because that answer has to be
                // 404 rather than 403 and an authorization failure cannot say
                // which of the two it meant.
                .AddPolicy(
                    AuthorizationPolicies.FaceStaticAsset, p => p
                        .RequireAuthenticatedUser()
                        .RequireScope(oauth.Qualify(ApiScopes.FaceRecognitionRead))
                );

        return services;
    }
}
