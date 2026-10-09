namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// Remembers keys for a while, e.g. "packet 123 from node X" so a packet heard by several gateways is processed once,
/// or "node X heard by gateway G" to write that at most once a minute. Not thread-safe: the ingest router is its
/// only user. Old keys are cleaned up as new ones arrive, so memory stays bounded by the traffic of one window.
/// </summary>
public sealed class PacketDeduplicator(TimeSpan window, TimeProvider timeProvider)
{
    /// <summary>Clean up after this many new keys (cheap enough, and keeps the dictionary small).</summary>
    private const int CleanupEvery = 1_000;

    private readonly Dictionary<(uint, uint), DateTimeOffset> _seen = [];
    private int _added;

    public int Count => _seen.Count;

    /// <summary>True the first time a key is seen within the window (and remembers it); false for repeats.</summary>
    public bool TryAdd(uint first, uint second)
    {
        var now = timeProvider.GetUtcNow();
        if (_seen.TryGetValue((first, second), out var seenAt) && now - seenAt < window)
        {
            return false;
        }

        _seen[(first, second)] = now;
        if (++_added % CleanupEvery == 0)
        {
            foreach (var key in _seen.Where(entry => now - entry.Value >= window).Select(entry => entry.Key).ToList())
            {
                _seen.Remove(key);
            }
        }

        return true;
    }
}
