namespace MawMedia.Models;

public class SearchResult<T>
{
    public IEnumerable<T> Results { get; private init; }
    public bool HasMoreResults { get; private init; }
    public int NextOffset { get; private init; }

    public SearchResult(IEnumerable<T> results, bool hasMoreResults, int nextOffset)
    {
        Results = results;
        HasMoreResults = hasMoreResults;
        NextOffset = nextOffset;
    }
}
