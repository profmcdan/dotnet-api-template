using DotnetApiTemplate.Cli.CommandLine;

namespace DotnetApiTemplate.Cli.UnitTests;

public sealed class ParsedArgumentsTests
{
    private static readonly OptionSet Options = new OptionSet()
        .Add("--project-name", "name", "-n")
        .Add("--api-port", "port", "-p")
        .AddFlag("--allow-grpc", "grpc", "-g")
        .AddFlag("--no-tests", "no tests");

    [Fact]
    public void Reads_a_long_option_with_a_separate_value()
    {
        var parsed = ParsedArguments.Parse(["--project-name", "Acme.Billing"], Options);

        parsed.GetString("--project-name").ShouldBe("Acme.Billing");
    }

    [Fact]
    public void Reads_a_long_option_written_with_an_equals_sign()
    {
        var parsed = ParsedArguments.Parse(["--project-name=Acme.Billing"], Options);

        parsed.GetString("--project-name").ShouldBe("Acme.Billing");
    }

    [Fact]
    public void Resolves_short_aliases()
    {
        var parsed = ParsedArguments.Parse(["-n", "Acme.Billing", "-g"], Options);

        parsed.GetString("--project-name").ShouldBe("Acme.Billing");
        parsed.GetFlag("--allow-grpc").ShouldBeTrue();
    }

    [Fact]
    public void Treats_a_bare_flag_as_true()
    {
        ParsedArguments.Parse(["--no-tests"], Options).GetFlag("--no-tests").ShouldBeTrue();
    }

    [Fact]
    public void Allows_a_flag_to_be_set_to_false_explicitly()
    {
        ParsedArguments.Parse(["--allow-grpc=false"], Options).GetFlag("--allow-grpc").ShouldBeFalse();
    }

    [Fact]
    public void Defaults_an_absent_flag_to_false()
    {
        ParsedArguments.Parse([], Options).GetFlag("--allow-grpc").ShouldBeFalse();
    }

    [Fact]
    public void Collects_positional_arguments()
    {
        var parsed = ParsedArguments.Parse(["Acme.Billing", "-g"], Options);

        parsed.Positional.ShouldBe(["Acme.Billing"]);
    }

    [Fact]
    public void Parses_integers()
    {
        ParsedArguments.Parse(["--api-port", "6000"], Options).GetInt("--api-port", 5080).ShouldBe(6000);
    }

    [Fact]
    public void Falls_back_when_an_integer_option_is_absent()
    {
        ParsedArguments.Parse([], Options).GetInt("--api-port", 5080).ShouldBe(5080);
    }

    [Fact]
    public void Rejects_a_non_numeric_value_for_an_integer_option()
    {
        var parsed = ParsedArguments.Parse(["--api-port", "quite-high"], Options);

        Should.Throw<CommandLineException>(() => parsed.GetInt("--api-port", 5080));
    }

    [Fact]
    public void Reports_a_value_option_left_dangling()
    {
        Should.Throw<CommandLineException>(() => ParsedArguments.Parse(["--project-name"], Options))
            .Message.ShouldContain("expects a value");
    }

    [Fact]
    public void Surfaces_unrecognised_options_rather_than_ignoring_them()
    {
        var parsed = ParsedArguments.Parse(["--typo", "value"], Options);

        parsed.Unrecognised.ShouldBe(["--typo"]);
    }

    [Fact]
    public void Option_names_are_case_insensitive_but_short_aliases_are_not()
    {
        ParsedArguments.Parse(["--PROJECT-NAME", "X"], Options).GetString("--project-name").ShouldBe("X");
        ParsedArguments.Parse(["-G"], Options).Unrecognised.ShouldBe(["-G"]);
    }
}
