using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CleanArchTemplate.Application.Auth;

namespace CleanArchTemplate.Api.IntegrationTests.Fixtures;

[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFactory factory)
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected ApiFactory Factory { get; } = factory;

    /// <summary>Skips the whole class when Docker is unavailable instead of failing the build.</summary>
    protected void RequireDocker() => Assert.SkipWhen(Factory.SkipReason is not null, Factory.SkipReason ?? string.Empty);

    protected HttpClient CreateClient() => Factory.CreateClient();

    protected async Task<HttpClient> CreateAdministratorClientAsync(CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var tokens = await SignInAsync(client, ApiFactory.AdministratorEmail, ApiFactory.AdministratorPassword, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    protected static async Task<AuthTokensResponse> SignInAsync(HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Json, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AuthTokensResponse>(Json, cancellationToken))!;
    }
}
