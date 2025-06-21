using NodaTime;

namespace MawMedia.Models;

public record Comment(
    Guid CommentId,
    Instant Created,
    string CreatedBy,
    Instant Modified,
    string Body
);
