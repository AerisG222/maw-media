namespace MawMedia.Routes.Extensions;

public static class HttpRequestExtensions
{
    // credit: https://blog.elmah.io/how-to-get-base-url-in-asp-net-core/
    public static string GetBaseUrl(this HttpRequest req)
    {
        var uriBuilder = new UriBuilder(
            req.Scheme,
            req.Host.Host,
            req.Host.Port ?? -1
        );

        if (uriBuilder.Uri.IsDefaultPort)
        {
            uriBuilder.Port = -1;
        }

        return uriBuilder.Uri.AbsoluteUri;
    }
}
