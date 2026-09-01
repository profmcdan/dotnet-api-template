using System.Net;
using CleanArchTemplate.Api.IntegrationTests.Fixtures;

namespace CleanArchTemplate.Api.IntegrationTests.Scenarios;

public sealed class HealthTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Liveness_does_not_depend_on_any_downstream_service()
    {
        RequireDocker();

        var response = await CreateClient().GetAsync("/health/live", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task Readiness_reports_the_dependencies_it_checked()
    {
        RequireDocker();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await CreateClient().GetAsync("/health/ready", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldContain("postgres");
        body.ShouldContain("redis");
    }
}
