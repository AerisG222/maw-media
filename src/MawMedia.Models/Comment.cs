using NodaTime;

namespace MawMedia.Models;

public sealed record Comment(
    Guid CommentId,
    Instant Created,
    string CreatedBy,
    Instant Modified,
    string Body
);
