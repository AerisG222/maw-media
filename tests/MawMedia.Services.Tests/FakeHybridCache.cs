using Microsoft.Extensions.Caching.Hybrid;

namespace MawMedia.Services.Tests;

// https://github.com/dotnet/extensions/issues/5763
sealed class FakeHybridCache : HybridCache
{
    public override ValueTask<T> GetOrCreateAsync<TState, T>(string key, TState state, Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
        => factory(state, cancellationToken);

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) => default;
    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => default;
    // every key written, so a test can see what a repository primed.  nothing is
    // ever read back from it - GetOrCreateAsync above always runs the factory - so
    // recording cannot change what any other test observes
    public IReadOnlyCollection<string> SetKeys => _setKeys;

    readonly List<string> _setKeys = [];

    public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions? options = null, IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        lock (_setKeys)
        {
            _setKeys.Add(key);
        }

        return default;
    }
}
