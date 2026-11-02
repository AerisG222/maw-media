using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Net.Http.Headers;

namespace MawMedia.Extensions;

// a browser cannot put a bearer token on an <img> or <video>, so it trades its
// token for this cookie and those requests carry that instead.
//
// the cookie is a narrower credential than the token it came from, on purpose:
//   - it is only read under /assets.  the default scheme stays JwtBearer, so no
//     api route accepts it, which is why it needs no csrf protection: everything
//     it can reach is a GET.
//   - it can not renew itself, because the route that issues it takes a bearer
//     token.  a cookie left alone simply expires.
//   - it holds the sub and the scopes the asset branches ask for and nothing else.
//     a stolen one reads files, and only until it expires.
//   - it never holds the media identity.  the claims transformation resolves that
//     per request, exactly as it does for a bearer token, so a change to a user
//     reaches a cookie as soon as it reaches a token.
public static class AssetSessionExtensions
{
    public const string Scheme = "AssetSession";

    // __Host- makes the browser refuse it unless it is Secure, host only and on
    // Path=/, so a sibling subdomain can not plant one of its own.
    const string CookieName = "__Host-maw-media-assets";

    // the upper limit on how long an unattended browser keeps reading files.
    // shorter than an access token can be, which is why it is capped separately.
    static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(1);

    public static AuthenticationBuilder AddAssetSessionCookie(this AuthenticationBuilder builder) =>
        builder.AddCookie(Scheme, opts =>
        {
            opts.Cookie.Name = CookieName;
            opts.Cookie.HttpOnly = true;
            opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            opts.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
            opts.Cookie.Path = "/";

            // renewing is the client's decision, made only while somebody is using
            // it.  sliding would let any asset request extend the session.
            opts.SlidingExpiration = false;
        });

    public static async Task<TimeSpan> SignInToAssetSession(
        this HttpContext ctx,
        ClaimsPrincipal user,
        OAuthConfig oauth
    )
    {
        var now = DateTimeOffset.UtcNow;
        var expires = GetExpiry(user, now);

        await ctx.SignInAsync(
            Scheme,
            CreatePrincipal(user, oauth),
            new AuthenticationProperties
            {
                AllowRefresh = false,
                ExpiresUtc = expires,
                IsPersistent = true
            }
        );

        return expires - now;
    }

    public static Task SignOutOfAssetSession(this HttpContext ctx) =>
        ctx.SignOutAsync(Scheme);

    // runs ahead of the three asset branches, which all authorize against ctx.User.
    public static IApplicationBuilder UseAssetSession(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            // a browser can load the same file both with and without cors, and the
            // cors headers are only on the response to a request that sent an Origin.
            // this keeps a cached copy of one from being offered to the other.  chrome
            // was seen to refetch without it, but that is not something to depend on.
            ctx.Response.Headers.Append(HeaderNames.Vary, HeaderNames.Origin);

            // a request with an Authorization header is judged by it alone, so a
            // bad token is refused rather than quietly rescued by a cookie.
            if (!ctx.Request.Headers.ContainsKey(HeaderNames.Authorization) &&
                ctx.User.Identity?.IsAuthenticated != true)
            {
                var result = await ctx.AuthenticateAsync(Scheme);

                if (result.Succeeded)
                {
                    ctx.User = result.Principal;
                }
            }

            await next(ctx);
        });

    static DateTimeOffset GetExpiry(ClaimsPrincipal user, DateTimeOffset now)
    {
        var limit = now + MaxLifetime;

        // never outlives the token that vouched for it
        return long.TryParse(user.FindFirstValue("exp"), NumberStyles.None, CultureInfo.InvariantCulture, out var exp) &&
            DateTimeOffset.FromUnixTimeSeconds(exp) is var tokenExpiry &&
            tokenExpiry < limit
                ? tokenExpiry
                : limit;
    }

    // built from nothing rather than copied, because `user` has already been
    // through the claims transformation and must not persist what it added.
    static ClaimsPrincipal CreatePrincipal(ClaimsPrincipal user, OAuthConfig oauth)
    {
        var assetScopes = new[]
        {
            oauth.Qualify(ApiScopes.MediaRead),
            oauth.Qualify(ApiScopes.FaceRecognitionRead)
        };

        var scopes = user.FindAll("scp")
            .Union(user.FindAll("scope"))
            .SelectMany(c => c.Value.Split(' '))
            .Intersect(assetScopes, StringComparer.Ordinal);

        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", user.FindFirstValue("sub") ?? string.Empty),
                new Claim("scope", string.Join(' ', scopes))
            ],
            Scheme
        ));
    }
}
