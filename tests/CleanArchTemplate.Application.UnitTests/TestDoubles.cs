using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Links;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;

namespace CleanArchTemplate.Application.UnitTests;

/// <summary>A clock the test owns, so time-dependent behaviour is asserted rather than waited for.</summary>
internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

/// <summary>In-memory cache with the real interface's semantics but none of its failure modes.</summary>
internal sealed class FakeCache : ICacheService
{
    private readonly Dictionary<string, object?> _entries = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> RemovedKeys => _removed;

    private readonly List<string> _removed = [];

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_entries.TryGetValue(key, out var value) ? (T?)value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        _entries[key] = value;
        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(key, out var cached) && cached is T typed)
        {
            return typed;
        }

        var value = await factory(cancellationToken);
        _entries[key] = value;
        return value;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.Remove(key);
        _removed.Add(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in _entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            _entries.Remove(key);
        }

        _removed.Add(prefix);
        return Task.CompletedTask;
    }
}

internal sealed class TestCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }

    public string? Email { get; set; }

    public IReadOnlyCollection<string> Roles { get; set; } = [];

    public bool IsAuthenticated => UserId is not null;

    public string? IpAddress { get; set; } = "203.0.113.10";

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.Ordinal);
}

/// <summary>Reversible stand-in for bcrypt: fast, and lets assertions look at what was hashed.</summary>
internal sealed class TestPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";

    public bool Verify(string password, string? hash) => hash == $"hashed:{password}";

    public bool NeedsRehash(string hash) => false;
}

internal sealed class TestLinkBuilder : IAppLinkBuilder
{
    public string AcceptInvitationUrl(string token) => $"https://app.test/accept-invitation?token={token}";

    public string SignInUrl() => "https://app.test/sign-in";

    public string SupportUrl() => "mailto:support@app.test";
}

internal sealed class TestTokenService : ITokenService
{
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(14);

    public AccessToken CreateAccessToken(Domain.Users.User user) =>
        new($"access:{user.Id}:{user.SecurityStamp}", DateTimeOffset.UtcNow.AddMinutes(15));
}
