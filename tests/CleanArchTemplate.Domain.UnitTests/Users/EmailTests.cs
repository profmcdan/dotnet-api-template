using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Domain.UnitTests.Users;

public sealed class EmailTests
{
    [Theory]
    [InlineData("someone@example.com")]
    [InlineData("first.last+tag@sub.example.co.uk")]
    [InlineData("x@y.io")]
    public void Create_accepts_valid_addresses(string candidate) =>
        Email.Create(candidate).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("no@tld")]
    [InlineData("two@@example.com")]
    [InlineData("spaces in@example.com")]
    public void Create_rejects_invalid_addresses(string candidate) =>
        Email.Create(candidate).IsFailure.ShouldBeTrue();

    [Fact]
    public void Create_normalises_case_and_whitespace()
    {
        var result = Email.Create("  Someone@Example.COM  ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("someone@example.com");
    }

    [Fact]
    public void Create_rejects_addresses_over_the_length_limit()
    {
        var local = new string('a', Email.MaxLength);

        Email.Create($"{local}@example.com").IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Equality_is_by_normalised_value()
    {
        var first = Email.Create("Person@Example.com").Value;
        var second = Email.Create("person@example.com").Value;

        first.ShouldBe(second);
    }

    [Fact]
    public void Domain_returns_the_part_after_the_at_sign() =>
        Email.Create("person@example.com").Value.Domain.ShouldBe("example.com");
}
