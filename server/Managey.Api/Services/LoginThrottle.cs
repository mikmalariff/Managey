using System.Collections.Concurrent;

namespace Managey.Api.Services;

/// <summary>
/// Slows down password guessing by counting failed sign-ins. Only failures count, so a whole office signing in
/// at 9am from one shared IP address is never blocked. Limits per account stop guessing against one user even
/// if the attacker spreads requests over many addresses; the per-address limit stops one client trying many accounts.
/// </summary>
public class LoginThrottle(TimeProvider clock)
{
    public const int MaxFailuresPerAccount = 10;
    public const int MaxFailuresPerAddress = 50;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTimeOffset>> _failures = new();

    public bool IsBlocked(string username, string? address) =>
        Count(AccountKey(username)) >= MaxFailuresPerAccount || Count(AddressKey(address)) >= MaxFailuresPerAddress;

    public void RecordFailure(string username, string? address)
    {
        var now = clock.GetUtcNow();
        _failures.GetOrAdd(AccountKey(username), _ => new()).Enqueue(now);
        _failures.GetOrAdd(AddressKey(address), _ => new()).Enqueue(now);
    }

    /// <summary>A successful sign-in clears the account's failures (not the address's).</summary>
    public void RecordSuccess(string username) => _failures.TryRemove(AccountKey(username), out _);

    private int Count(string key)
    {
        if (!_failures.TryGetValue(key, out var queue)) return 0;
        var cutoff = clock.GetUtcNow() - Window;
        while (queue.TryPeek(out var oldest) && oldest < cutoff) queue.TryDequeue(out _);
        if (queue.IsEmpty) _failures.TryRemove(key, out _);
        return queue.Count;
    }

    private static string AccountKey(string username) => "u:" + username.Trim().ToLowerInvariant();
    private static string AddressKey(string? address) => "a:" + (address ?? "unknown");
}
