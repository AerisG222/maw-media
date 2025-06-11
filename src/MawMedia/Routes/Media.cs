namespace MawMedia.Routes;

public static class Media
{
    public static RouteGroupBuilder MapMediaRoutes(this RouteGroupBuilder group)
    {
        group.MapGet("/", (HttpRequest request, ILogger<Program> log) => {
            return "media";
        })
        .WithName("media")
        .WithSummary("Media")
        .WithDescription("Lists media");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        return group;
    }
}
