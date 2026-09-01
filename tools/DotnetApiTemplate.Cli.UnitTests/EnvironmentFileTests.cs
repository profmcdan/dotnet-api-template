using System.Text.RegularExpressions;
using DotnetApiTemplate.Cli.Generation;

namespace DotnetApiTemplate.Cli.UnitTests;

public sealed class EnvironmentFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("cli-env-tests").FullName;

    private const string Sample = """
        # a comment that mentions JWT__SIGNINGKEY= and must not be rewritten
        POSTGRES_DB=appdb
        POSTGRES_USER=appuser
        POSTGRES_PASSWORD=change-me-in-production
        DATABASE__CONNECTIONSTRING=Host=postgres;Port=5432;Database=appdb;Username=appuser;Password=change-me-in-production;Include Error Detail=true
        JWT__SIGNINGKEY=replace-this-with-a-real-secret-at-least-32-chars-long
        JWT__ISSUER=cleanarch-template
        """;

    private void WriteSample() => File.WriteAllText(Path.Combine(_directory, ".env.sample"), Sample);

    private string ReadEnv() => File.ReadAllText(Path.Combine(_directory, ".env"));

    [Fact]
    public void Creates_env_from_the_sample()
    {
        WriteSample();

        var result = EnvironmentFile.Create(_directory);

        result.Created.ShouldBeTrue();
        File.Exists(result.Path).ShouldBeTrue();
    }

    [Fact]
    public void Replaces_the_placeholder_signing_key_with_a_usable_one()
    {
        WriteSample();
        EnvironmentFile.Create(_directory);

        var key = Value("JWT__SIGNINGKEY");

        key.ShouldNotBe("replace-this-with-a-real-secret-at-least-32-chars-long");
        key.Length.ShouldBeGreaterThanOrEqualTo(32);
    }

    [Fact]
    public void Keeps_the_database_password_and_the_connection_string_in_step()
    {
        WriteSample();
        EnvironmentFile.Create(_directory);

        var password = Value("POSTGRES_PASSWORD");
        var connectionString = Value("DATABASE__CONNECTIONSTRING");

        password.ShouldNotBe("change-me-in-production");
        connectionString.ShouldContain($"Password={password};");
        connectionString.ShouldNotContain("change-me-in-production");
    }

    [Fact]
    public void Generates_a_password_that_needs_no_escaping_anywhere()
    {
        WriteSample();
        EnvironmentFile.Create(_directory);

        // It travels through a connection string, a shell env file and a URL.
        Value("POSTGRES_PASSWORD").ShouldMatch("^[A-Za-z0-9]+$");
    }

    [Fact]
    public void Leaves_unrelated_settings_alone()
    {
        WriteSample();
        EnvironmentFile.Create(_directory);

        Value("POSTGRES_DB").ShouldBe("appdb");
        Value("JWT__ISSUER").ShouldBe("cleanarch-template");
    }

    [Fact]
    public void Does_not_rewrite_comments()
    {
        WriteSample();
        EnvironmentFile.Create(_directory);

        ReadEnv().ShouldContain("# a comment that mentions JWT__SIGNINGKEY= and must not be rewritten");
    }

    [Fact]
    public void Never_overwrites_an_existing_env()
    {
        WriteSample();
        File.WriteAllText(Path.Combine(_directory, ".env"), "MINE=1");

        var result = EnvironmentFile.Create(_directory);

        result.Created.ShouldBeFalse();
        result.SkipReason.ShouldNotBeNull();
        ReadEnv().ShouldBe("MINE=1");
    }

    [Fact]
    public void Reports_a_missing_sample_rather_than_throwing()
    {
        var result = EnvironmentFile.Create(_directory);

        result.Created.ShouldBeFalse();
        result.SkipReason.ShouldContain(".env.sample");
    }

    [Fact]
    public void Generates_a_different_secret_every_time()
    {
        WriteSample();
        EnvironmentFile.Create(_directory);
        var first = Value("JWT__SIGNINGKEY");

        File.Delete(Path.Combine(_directory, ".env"));
        EnvironmentFile.Create(_directory);

        Value("JWT__SIGNINGKEY").ShouldNotBe(first);
    }

    private string Value(string key) =>
        Regex.Match(ReadEnv(), $"^{Regex.Escape(key)}=(.*)$", RegexOptions.Multiline).Groups[1].Value.Trim();

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
