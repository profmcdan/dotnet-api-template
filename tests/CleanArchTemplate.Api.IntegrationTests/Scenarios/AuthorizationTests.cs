using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CleanArchTemplate.Api.IntegrationTests.Fixtures;
using CleanArchTemplate.Application.Auth;
using CleanArchTemplate.Application.Users.Queries;

namespace CleanArchTemplate.Api.IntegrationTests.Scenarios;

public sealed class AuthorizationTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Anonymous_callers_cannot_read_the_user_list()
    {
        RequireDocker();

        var response = await CreateClient().GetAsync("/api/v1/users", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_member_cannot_invite_other_users()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;
        var memberClient = await CreateMemberClientAsync(cancellationToken);

        var response = await memberClient.PostAsJsonAsync(
            "/api/v1/users/invitations",
            new { email = "someone@example.com", fullName = "Someone", roles = new[] { "member" } },
            Json,
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_member_can_still_read_their_own_profile()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;
        var memberClient = await CreateMemberClientAsync(cancellationToken);

        var response = await memberClient.GetAsync("/api/v1/users/me", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Suspending_a_user_invalidates_their_existing_access_token()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;
        var admin = await CreateAdministratorClientAsync(cancellationToken);
        var (memberClient, memberId) = await CreateMemberAsync(cancellationToken);

        // The token still has minutes left on it - only the security stamp check evicts it.
        (await memberClient.GetAsync("/api/v1/users/me", cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var suspend = await admin.PostAsJsonAsync(
            $"/api/v1/users/{memberId}/suspend", new { reason = "Testing" }, Json, cancellationToken);
        suspend.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterSuspension = await memberClient.GetAsync("/api/v1/users/me", cancellationToken);
        afterSuspension.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_administrator_cannot_suspend_their_own_account()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;
        var admin = await CreateAdministratorClientAsync(cancellationToken);

        var me = await admin.GetFromJsonAsync<UserResponse>("/api/v1/users/me", Json, cancellationToken);

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/users/{me!.Id}/suspend", new { reason = "Oops" }, Json, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_refresh_token_rotates_and_the_old_one_stops_working()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient();

        var first = await SignInAsync(client, ApiFactory.AdministratorEmail, ApiFactory.AdministratorPassword, cancellationToken);

        var refreshed = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = first.RefreshToken }, Json, cancellationToken);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Replaying the consumed token is treated as theft, not as a retry.
        var replay = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = first.RefreshToken }, Json, cancellationToken);
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> CreateMemberClientAsync(CancellationToken cancellationToken) =>
        (await CreateMemberAsync(cancellationToken)).Client;

    private async Task<(HttpClient Client, Guid UserId)> CreateMemberAsync(CancellationToken cancellationToken)
    {
        var admin = await CreateAdministratorClientAsync(cancellationToken);
        var email = $"member-{Guid.CreateVersion7():N}@example.com";
        const string password = "member-strong-pass-1";

        var invite = await admin.PostAsJsonAsync(
            "/api/v1/users/invitations",
            new { email, fullName = "Member User", roles = new[] { "member" } },
            Json,
            cancellationToken);
        invite.EnsureSuccessStatusCode();

        var created = (await invite.Content.ReadFromJsonAsync<UserResponse>(Json, cancellationToken))!;
        var token = await InvitationTokenReader.ReadAsync(Factory, created.Id, cancellationToken);

        var anonymous = CreateClient();
        var accept = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/invitations/accept",
            new { token, password, fullName = (string?)null },
            Json,
            cancellationToken);
        accept.EnsureSuccessStatusCode();

        var tokens = (await accept.Content.ReadFromJsonAsync<AuthTokensResponse>(Json, cancellationToken))!;

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (client, created.Id);
    }
}
