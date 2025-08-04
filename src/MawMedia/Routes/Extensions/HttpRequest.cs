using Microsoft.AspNetCore.Http.Extensions;

namespace MawMedia.Routes.Extensions;

public static class HttpRequestExtensions
{
    public static string GetBaseUrl(this HttpRequest request) =>
        request
            .GetEncodedUrl()
            .Replace(request.Path, string.Empty);
}
