using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace MawMedia.Extensions;

public static class OpenApiExtensions
{
    const string TITLE = "MaW Media API";
    const string DESCRIPTION = "An API to access photos and videos from media.mikeandwan.us.";

    const string SCHEME_OAUTH2 = "OAuth2";
    const string SCHEME_BEARER = "Bearer";

    public static IServiceCollection AddCustomOpenApi(this IServiceCollection services)
    {
        services.Configure<RouteOptions>(options => options.SetParameterPolicy<RegexInlineRouteConstraint>("regex"));

        // register one OpenAPI document per API version. the ApiExplorer assigns each
        // versioned endpoint a GroupName ("v1", "v2", ...) matching the document name,
        // so the native document filter includes only that version's endpoints.
        foreach (var version in ApiVersioningExtensions.All)
        {
            var documentName = ApiVersioningExtensions.DocumentName(version);

            services.AddOpenApi(documentName, opts =>
            {
                opts.AddDocumentTransformer((document, context, _) =>
                {
                    var oauth = context.ApplicationServices.GetRequiredService<IOptions<OAuthConfig>>().Value;

                    document.Info.Title = TITLE;
                    document.Info.Version = documentName;
                    document.Info.Description = DESCRIPTION;

                    document.Components ??= new();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                    // raw token paste - useful when you already have a JWT in hand
                    document.Components.SecuritySchemes[SCHEME_BEARER] = new OpenApiSecurityScheme
                    {
                        BearerFormat = "JSON Web Token",
                        Description = "Bearer authentication using a JWT.",
                        Scheme = SCHEME_BEARER,
                        Type = SecuritySchemeType.Http
                    };

                    // interactive login - lets Scalar drive the full authorization code + PKCE round trip
                    document.Components.SecuritySchemes[SCHEME_OAUTH2] = new OpenApiSecurityScheme
                    {
                        Description = "Authorization code flow with PKCE.",
                        Type = SecuritySchemeType.OAuth2,
                        Flows = new OpenApiOAuthFlows
                        {
                            AuthorizationCode = new OpenApiOAuthFlow
                            {
                                AuthorizationUrl = new Uri(oauth.AuthorizationUrl),
                                TokenUrl = new Uri(oauth.TokenUrl),
                                Scopes = oauth.QualifiedScopes()
                            }
                        }
                    };

                    // attach the scope each endpoint requires, derived from its authorization
                    // policy. this must happen in the document transformer (not an operation
                    // transformer): the scheme references need `document` as their host to
                    // resolve, otherwise the security requirement serializes as an empty {}.
                    ApplyOperationSecurity(document, context, oauth);

                    return Task.CompletedTask;
                });
            });
        }

        services.AddEndpointsApiExplorer();

        return services;
    }

    public static IApplicationBuilder UseCustomOpenApi(this IApplicationBuilder app)
    {
        var webApp = (WebApplication)app;
        var oauth = webApp.Services.GetRequiredService<IOptions<OAuthConfig>>().Value;

        // these sit outside the api group and so pick up none of its authorization. the
        // AllowAnonymous is kept explicit: the docs must be reachable without a token,
        // otherwise there is no page from which to perform the interactive login.
        webApp.MapOpenApi().AllowAnonymous();
        webApp.MapScalarApiReference(opts =>
        {
            opts.EnablePersistentAuthentication();

            opts.AddPreferredSecuritySchemes(
                string.IsNullOrWhiteSpace(oauth.ScalarClientId) ? SCHEME_BEARER : SCHEME_OAUTH2
            );

            if (string.IsNullOrWhiteSpace(oauth.ScalarClientId))
            {
                return;
            }

            opts.AddAuthorizationCodeFlow(SCHEME_OAUTH2, flow =>
            {
                flow.ClientId = oauth.ScalarClientId;
                flow.Pkce = Pkce.Sha256;
                flow.SelectedScopes = [.. oauth.QualifiedScopes().Keys];

                // Auth0 only mints a JWT for a custom API when the authorize request
                // carries the API identifier as `audience`. without it you get an
                // opaque token that JwtBearer validation will reject.
                flow.AdditionalQueryParameters = new Dictionary<string, string>
                {
                    ["audience"] = oauth.Audience
                };
            });
        }).AllowAnonymous();

        return webApp;
    }

    // sets a per-operation security requirement (OAuth2 with the endpoint's scopes, plus
    // Bearer) for every endpoint that declares an authorization policy. matches OpenAPI
    // operations to their ApiDescription by route + method so the correct scopes land on
    // each operation. the scheme references are created with `document` as host so they
    // serialize to their scheme name rather than an empty object.
    static void ApplyOperationSecurity(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        OAuthConfig oauth
    )
    {
        var scopesByRoute = new Dictionary<(string Path, string Method), List<string>>();

        foreach (var group in context.DescriptionGroups)
        {
            foreach (var api in group.Items)
            {
                if (string.IsNullOrWhiteSpace(api.RelativePath) || string.IsNullOrWhiteSpace(api.HttpMethod))
                {
                    continue;
                }

                var policies = api.ActionDescriptor.EndpointMetadata
                    .OfType<IAuthorizeData>()
                    .Select(a => a.Policy)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToArray();

                if (policies.Length == 0)
                {
                    continue;
                }

                var scopes = policies
                    .Where(p => ApiScopes.ByPolicy.ContainsKey(p!))
                    .Select(p => oauth.Qualify(ApiScopes.ByPolicy[p!]))
                    .Distinct()
                    .ToList();

                scopesByRoute[(NormalizePath(api.RelativePath), api.HttpMethod.ToUpperInvariant())] = scopes;
            }
        }

        if (document.Paths is null)
        {
            return;
        }

        foreach (var (path, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var (method, operation) in pathItem.Operations)
            {
                if (!scopesByRoute.TryGetValue((NormalizePath(path), method.Method.ToUpperInvariant()), out var scopes))
                {
                    continue;
                }

                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SCHEME_OAUTH2, document)] = scopes,
                    [new OpenApiSecuritySchemeReference(SCHEME_BEARER, document)] = []
                });
            }
        }
    }

    // canonicalizes a route path for matching: single leading slash, no trailing slash,
    // so a group-root route ("api/v1/upload/") matches the document's key ("/api/v1/upload").
    static string NormalizePath(string path) =>
        "/" + path.Trim('/');
}
