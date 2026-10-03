namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// How long to wait before the next connection attempt: 1 s, 2 s, 4 s … up to 5 minutes, with ±20 % jitter so
/// we do not hammer a node that is rebooting, and do not retry in lock-step with other clients.
/// </summary>
internal static class ReconnectBackoff
{
    public static readonly TimeSpan Initial = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan Maximum = TimeSpan.FromMinutes(5);

    /// <param name="attempt">0 for the first retry.</param>
    /// <param name="random01">A random number in [0, 1); passed in so tests are repeatable.</param>
    public static TimeSpan Delay(int attempt, double random01)
    {
        var exponent = Math.Min(attempt, 20);
        var baseDelay = Math.Min(Initial.TotalMilliseconds * Math.Pow(2, exponent), Maximum.TotalMilliseconds);
        var jitter = 0.8 + (random01 * 0.4);
        return TimeSpan.FromMilliseconds(Math.Min(baseDelay * jitter, Maximum.TotalMilliseconds));
    }
}
