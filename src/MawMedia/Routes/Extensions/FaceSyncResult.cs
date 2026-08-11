using MawMedia.Models.FaceRecognition;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MawMedia.Routes.Extensions;

public static class FaceSyncResultExtensions
{
    const string OUTCOME_FORBIDDEN = "forbidden";

    // the media.sync_* / media.delete_* functions answer with a single
    // 'forbidden' row when the caller is not an admin.  returning 200 with a
    // body that says forbidden would be a poor contract, so it is promoted to a
    // real 403 here.  every other outcome is per item and legitimately part of a
    // successful response - one unresolved path does not make the batch a
    // failure.
    public static Results<Ok<IEnumerable<FaceSyncResult>>, BadRequest<string>, ForbidHttpResult> ToHttpResult(
        this IEnumerable<FaceSyncResult> results
    )
    {
        var materialized = results as IReadOnlyList<FaceSyncResult> ?? [.. results];

        return materialized.Count == 1 && materialized[0].Outcome == OUTCOME_FORBIDDEN
            ? TypedResults.Forbid()
            : TypedResults.Ok(materialized.AsEnumerable());
    }
}
