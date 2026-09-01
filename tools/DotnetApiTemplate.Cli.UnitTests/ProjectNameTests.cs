using DotnetApiTemplate.Cli.Generation;

namespace DotnetApiTemplate.Cli.UnitTests;

public sealed class ProjectNameTests
{
    [Theory]
    [InlineData("MyService", "MyService")]
    [InlineData("Acme.Billing", "Acme.Billing")]
    [InlineData("Acme.Billing.Api", "Acme.Billing.Api")]
    [InlineData("_internal.Tool2", "_internal.Tool2")]
    [InlineData("  Acme.Billing  ", "Acme.Billing")]
    [InlineData(".Acme.Billing.", "Acme.Billing")]
    [InlineData("Acme..Billing", "Acme.Billing")]
    public void Accepts_and_normalises_valid_names(string input, string expected)
    {
        ProjectName.TryValidate(input, out var normalised, out var error).ShouldBeTrue(error);
        normalised.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("9Leading.Digit")]
    [InlineData("has space")]
    [InlineData("has-hyphen")]
    [InlineData("Acme.Billing!")]
    [InlineData("...")]
    public void Rejects_names_that_are_not_valid_namespaces(string? input) =>
        ProjectName.TryValidate(input, out _, out _).ShouldBeFalse();

    [Theory]
    [InlineData("class")]
    [InlineData("Acme.namespace")]
    [InlineData("static.Thing")]
    public void Rejects_csharp_keywords_because_they_cannot_be_namespace_segments(string input)
    {
        ProjectName.TryValidate(input, out _, out var error).ShouldBeFalse();
        error.ShouldNotBeNull();
        error.ShouldContain("keyword");
    }

    [Fact]
    public void Rejects_an_absurdly_long_name() =>
        ProjectName.TryValidate(new string('A', 101), out _, out _).ShouldBeFalse();

    [Fact]
    public void Reports_a_reason_for_every_rejection()
    {
        ProjectName.TryValidate("has space", out _, out var error);
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("Acme.Billing", "acme-billing")]
    [InlineData("MyService", "myservice")]
    [InlineData("Contoso.Orders.Api", "contoso-orders-api")]
    public void Derives_a_kafka_safe_topic_prefix(string projectName, string expected) =>
        ProjectName.ToDefaultTopicPrefix(projectName).ShouldBe(expected);
}
