namespace MawMedia.Models;

// how long the cookie just issued lasts.  relative rather than a timestamp so a
// client with a wrong clock still renews on time.
public record AssetSession(
    int ExpiresInSeconds
);
