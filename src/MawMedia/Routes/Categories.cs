namespace MawMedia.Routes;

public static class Categories
{
    public static RouteGroupBuilder MapCategoryRoutes(this RouteGroupBuilder group)
    {
        group.MapGet("/", (HttpRequest request, ILogger<Program> log) => {
            return "categories";
        })
        .WithName("categories")
        .WithSummary("Categories")
        .WithDescription("Lists categories");
        // .RequireAuthorization(AuthorizationPolicies.Reader);

        return group;
    }
}
