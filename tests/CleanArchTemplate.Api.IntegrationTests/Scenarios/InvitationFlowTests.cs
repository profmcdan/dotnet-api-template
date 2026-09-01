using System.Net;
using System.Net.Http.Json;
using CleanArchTemplate.Api.IntegrationTests.Fixtures;
using CleanArchTemplate.Application.Auth;
using CleanArchTemplate.Application.Users.Queries;
using CleanArchTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchTemplate.Api.IntegrationTests.Scenarios;

/// <summary>
/// End-to-end cover for the flow the template exists to demonstrate: an administrator invites
/// someone, the invite is queued for email transactionally, and the invitee turns the token into
/// a working account.
/// </summary>
public sealed class InvitationFlowTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Inviting_a_user_writes_the_user_and_its_outbox_message_in_one_transaction()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await CreateAdministratorClientAsync(cancellationToken);
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync(
            "/api/v1/users/invitations",
            new { email, fullName = "Ada Lovelace", roles = new[] { "member" } },
            Json,
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(cancellationToken));
        var created = (await response.Content.ReadFromJsonAsync<UserResponse>(Json, cancellationToken))!;

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var invitation = await context.Invitations.AsNoTracking()
            .SingleOrDefaultAsync(i => i.UserId == created.Id, cancellationToken);
        invitation.ShouldNotBeNull();

        // The whole point of the outbox: the message exists because the user does.
        var (topic, payload) = await InvitationTokenReader.ReadMessageAsync(Factory, created.Id, cancellationToken);

        topic.ShouldBe($"{ApiFactory.TopicPrefix}.user.invited");
        payload.ShouldContain("https://app.test/accept-invitation?token=");
    }

    [Fact]
    public async Task An_invited_user_can_accept_and_is_signed_in_immediately()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await CreateAdministratorClientAsync(cancellationToken);
        var email = UniqueEmail();

        var created = await InviteAsync(client, email, cancellationToken);
        var token = await ReadInvitationTokenAsync(Factory, created.Id, cancellationToken);

        var anonymous = CreateClient();
        var accept = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/invitations/accept",
            new { token, password = "invitee-strong-pass-1", fullName = "Augusta King" },
            Json,
            cancellationToken);

        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = (await accept.Content.ReadFromJsonAsync<AuthTokensResponse>(Json, cancellationToken))!;

        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.User.Email.ShouldBe(email);
        tokens.User.FullName.ShouldBe("Augusta King");
        tokens.User.Status.ShouldBe(Domain.Users.UserStatus.Active);
    }

    [Fact]
    public async Task An_invitation_token_cannot_be_used_twice()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await CreateAdministratorClientAsync(cancellationToken);

        var created = await InviteAsync(client, UniqueEmail(), cancellationToken);
        var token = await ReadInvitationTokenAsync(Factory, created.Id, cancellationToken);

        var anonymous = CreateClient();
        var body = new { token, password = "invitee-strong-pass-1", fullName = (string?)null };

        var first = await anonymous.PostAsJsonAsync("/api/v1/auth/invitations/accept", body, Json, cancellationToken);
        var second = await anonymous.PostAsJsonAsync("/api/v1/auth/invitations/accept", body, Json, cancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_unknown_token_is_indistinguishable_from_an_expired_one()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await CreateClient().GetAsync("/api/v1/auth/invitations/definitely-not-a-real-token", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Inviting_the_same_address_twice_is_a_conflict()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await CreateAdministratorClientAsync(cancellationToken);
        var email = UniqueEmail();

        await InviteAsync(client, email, cancellationToken);

        var second = await client.PostAsJsonAsync(
            "/api/v1/users/invitations",
            new { email, fullName = "Duplicate", roles = new[] { "member" } },
            Json,
            cancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_weak_password_is_rejected_with_a_validation_problem()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await CreateAdministratorClientAsync(cancellationToken);

        var created = await InviteAsync(client, UniqueEmail(), cancellationToken);
        var token = await ReadInvitationTokenAsync(Factory, created.Id, cancellationToken);

        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/invitations/accept",
            new { token, password = "short", fullName = (string?)null },
            Json,
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<UserResponse> InviteAsync(HttpClient client, string email, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/users/invitations",
            new { email, fullName = "Ada Lovelace", roles = new[] { "member" } },
            Json,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponse>(Json, cancellationToken))!;
    }

    private static Task<string> ReadInvitationTokenAsync(ApiFactory factory, Guid userId, CancellationToken cancellationToken) =>
        InvitationTokenReader.ReadAsync(factory, userId, cancellationToken);

    private static string UniqueEmail() => $"invitee-{Guid.CreateVersion7():N}@example.com";
}
