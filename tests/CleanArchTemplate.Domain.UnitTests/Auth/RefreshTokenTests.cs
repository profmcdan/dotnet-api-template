using CleanArchTemplate.Domain.Auth;

namespace CleanArchTemplate.Domain.UnitTests.Auth;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    [Fact]
    public void Issue_stores_only_the_hash()
    {
        var (token, raw) = RefreshToken.Issue(Guid.CreateVersion7(), Lifetime, Now, "203.0.113.10");

        token.TokenHash.ShouldBe(RefreshToken.HashOf(raw));
        token.TokenHash.ShouldNotBe(raw);
        token.IsActiveAt(Now).ShouldBeTrue();
    }

    [Fact]
    public void A_token_is_inactive_once_it_expires()
    {
        var (token, _) = RefreshToken.Issue(Guid.CreateVersion7(), Lifetime, Now, null);

        token.IsActiveAt(Now.Add(Lifetime)).ShouldBeFalse();
    }

    [Fact]
    public void Rotation_revokes_the_old_token_and_links_it_to_its_replacement()
    {
        var userId = Guid.CreateVersion7();
        var (original, _) = RefreshToken.Issue(userId, Lifetime, Now, null);
        var (replacement, _) = RefreshToken.Issue(userId, Lifetime, Now, null, original.ChainId);

        var result = original.Rotate(replacement, Now.AddMinutes(5));

        result.IsSuccess.ShouldBeTrue();
        original.IsActiveAt(Now.AddMinutes(5)).ShouldBeFalse();
        original.ReplacedByTokenId.ShouldBe(replacement.Id);
        original.RevokedReason.ShouldBe("rotated");
    }

    [Fact]
    public void Rotating_an_already_rotated_token_fails()
    {
        var userId = Guid.CreateVersion7();
        var (original, _) = RefreshToken.Issue(userId, Lifetime, Now, null);
        var (first, _) = RefreshToken.Issue(userId, Lifetime, Now, null, original.ChainId);
        var (second, _) = RefreshToken.Issue(userId, Lifetime, Now, null, original.ChainId);

        original.Rotate(first, Now.AddMinutes(1));

        original.Rotate(second, Now.AddMinutes(2)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Rotation_keeps_the_chain_id_so_a_reuse_can_revoke_the_whole_family()
    {
        var userId = Guid.CreateVersion7();
        var (original, _) = RefreshToken.Issue(userId, Lifetime, Now, null);
        var (replacement, _) = RefreshToken.Issue(userId, Lifetime, Now, null, original.ChainId);

        replacement.ChainId.ShouldBe(original.ChainId);
    }

    [Fact]
    public void Revoke_is_idempotent_and_keeps_the_first_reason()
    {
        var (token, _) = RefreshToken.Issue(Guid.CreateVersion7(), Lifetime, Now, null);

        token.Revoke(Now.AddMinutes(1), "logout");
        token.Revoke(Now.AddMinutes(2), "password-changed");

        token.RevokedAt.ShouldBe(Now.AddMinutes(1));
        token.RevokedReason.ShouldBe("logout");
    }
}
