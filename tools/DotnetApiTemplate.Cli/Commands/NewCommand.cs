using DotnetApiTemplate.Cli.CommandLine;
using DotnetApiTemplate.Cli.Generation;

namespace DotnetApiTemplate.Cli.Commands;

/// <summary>
/// <c>dotnet-api-template new</c> - scaffolds a project.
/// <para>
/// The heavy lifting is the SDK's: this validates the inputs, translates the friendlier flags
/// into <c>dotnet new</c> arguments, and then does the setup a fresh project always needs anyway -
/// real secrets in <c>.env</c> and an initial commit.
/// </para>
/// </summary>
public static class NewCommand
{
    public const string Name = "new";

    public static OptionSet Options { get; } = new OptionSet()
        .Add("--project-name", "Name of the project. Becomes the solution name, every project name, assembly name and root namespace.", "-n", "name")
        .Add("--output", "Directory to create the project in.", "-o", "path", "current directory")
        .Add("--kafka-topic-prefix", "Prefix for every Kafka topic, the consumer group and the Redis key namespace.", "-k", "prefix", "derived from the project name")
        .Add("--api-port", "Host port Docker Compose publishes the API on.", "-p", "port", "5080")
        .Add("--database-name", "PostgreSQL database name.", "-d", "name", "appdb")
        .AddFlag("--allow-grpc", "Add a gRPC surface alongside REST, sharing the same query handlers.", "-g")
        .AddFlag("--no-tests", "Omit the unit and integration test projects.")
        .AddFlag("--no-docs", "Omit the docs/ folder.")
        .AddFlag("--no-env", "Do not create .env with generated secrets.")
        .AddFlag("--no-git", "Do not run git init and the first commit.")
        .AddFlag("--no-restore", "Skip the automatic dotnet restore.")
        .AddFlag("--force", "Overwrite an existing directory.")
        .AddFlag("--dry-run", "Print what would happen without writing anything.")
        .AddFlag("--help", "Show this help.", "-h");

    public static async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var parsed = ParsedArguments.Parse(args, Options);

        if (parsed.GetFlag("--help"))
        {
            WriteHelp();
            return ExitCodes.Success;
        }

        foreach (var unknown in parsed.Unrecognised)
        {
            Console.Error.WriteLine($"Unknown option '{unknown}'. Run 'dotnet-api-template new --help'.");
            return ExitCodes.UsageError;
        }

        // A bare positional is a friendly shorthand: `dotnet-api-template new Acme.Billing`.
        var rawName = parsed.GetString("--project-name") ?? (parsed.Positional.Count > 0 ? parsed.Positional[0] : null);

        if (!ProjectName.TryValidate(rawName, out var projectName, out var nameError))
        {
            Console.Error.WriteLine(nameError);
            return ExitCodes.UsageError;
        }

        var outputRoot = Path.GetFullPath(parsed.GetString("--output") ?? Directory.GetCurrentDirectory());
        var projectDirectory = Path.Combine(outputRoot, projectName);
        var topicPrefix = parsed.GetString("--kafka-topic-prefix") ?? ProjectName.ToDefaultTopicPrefix(projectName);
        var apiPort = parsed.GetInt("--api-port", 5080);
        var databaseName = parsed.GetString("--database-name") ?? "appdb";
        var allowGrpc = parsed.GetFlag("--allow-grpc");
        var includeTests = !parsed.GetFlag("--no-tests");
        var includeDocs = !parsed.GetFlag("--no-docs");
        var force = parsed.GetFlag("--force");
        var dryRun = parsed.GetFlag("--dry-run");

        if (apiPort is < 1 or > 65535)
        {
            Console.Error.WriteLine($"--api-port must be between 1 and 65535, but was {apiPort}.");
            return ExitCodes.UsageError;
        }

        if (Directory.Exists(projectDirectory) && !force && Directory.EnumerateFileSystemEntries(projectDirectory).Any())
        {
            Console.Error.WriteLine($"'{projectDirectory}' already exists and is not empty. Pass --force to overwrite.");
            return ExitCodes.UsageError;
        }

        Console.WriteLine();
        Console.WriteLine($"  Project name   {projectName}");
        Console.WriteLine($"  Location       {projectDirectory}");
        Console.WriteLine($"  Topic prefix   {topicPrefix}");
        Console.WriteLine($"  Database       {databaseName}");
        Console.WriteLine($"  API port       {apiPort}");
        Console.WriteLine($"  gRPC           {(allowGrpc ? "enabled" : "disabled")}");
        Console.WriteLine($"  Tests / docs   {(includeTests ? "tests" : "no tests")}, {(includeDocs ? "docs" : "no docs")}");
        Console.WriteLine();

        var templateArguments = BuildTemplateArguments(
            projectName, projectDirectory, topicPrefix, apiPort, databaseName,
            allowGrpc, includeTests, includeDocs, parsed.GetFlag("--no-restore"), force);

        if (dryRun)
        {
            Console.WriteLine("Dry run - nothing was written. The command would be:");
            Console.WriteLine();
            Console.WriteLine($"  dotnet {string.Join(' ', templateArguments)}");
            Console.WriteLine();
            return ExitCodes.Success;
        }

        if (await TemplatePackage.EnsureInstalledAsync(force: false, cancellationToken))
        {
            Console.WriteLine($"Registered template version {TemplatePackage.Version}.");
        }

        Console.WriteLine("Generating…");
        var generation = await ProcessRunner.DotnetAsync(templateArguments, cancellationToken: cancellationToken);

        if (!generation.Succeeded)
        {
            Console.Error.WriteLine(generation.CombinedOutput);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Generation failed. Nothing was left behind unless the output above says otherwise.");
            return ExitCodes.GenerationFailed;
        }

        await RunPostGenerationAsync(parsed, projectDirectory, cancellationToken);

        WriteNextSteps(projectName, projectDirectory, apiPort);
        return ExitCodes.Success;
    }

    private static List<string> BuildTemplateArguments(
        string projectName, string projectDirectory, string topicPrefix, int apiPort, string databaseName,
        bool allowGrpc, bool includeTests, bool includeDocs, bool skipRestore, bool force)
    {
        var arguments = new List<string>
        {
            "new", TemplatePackage.ShortName,
            "--name", projectName,
            "--output", projectDirectory,
            "--kafka-topic-prefix", topicPrefix,
            "--api-port", apiPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--database-name", databaseName,
            "--allow-grpc", allowGrpc ? "true" : "false",
            "--include-tests", includeTests ? "true" : "false",
            "--include-docs", includeDocs ? "true" : "false",
        };

        if (skipRestore)
        {
            arguments.Add("--skip-restore");
            arguments.Add("true");
        }

        if (force)
        {
            arguments.Add("--force");
        }

        return arguments;
    }

    private static async Task RunPostGenerationAsync(ParsedArguments parsed, string projectDirectory, CancellationToken cancellationToken)
    {
        if (!parsed.GetFlag("--no-env"))
        {
            var env = EnvironmentFile.Create(projectDirectory);

            if (env.Created)
            {
                Console.WriteLine("Wrote .env with a freshly generated signing key and database password.");
            }
            else if (env.SkipReason is { } reason)
            {
                Console.WriteLine($"Skipped .env: {reason}.");
            }
        }

        if (!parsed.GetFlag("--no-git"))
        {
            var problem = await GitRepository.InitialiseAsync(projectDirectory, cancellationToken);

            if (problem is null)
            {
                Console.WriteLine("Initialised a git repository with the scaffold as its first commit.");
            }
            else
            {
                Console.WriteLine($"Skipped git setup: {problem.Split('\n')[0]}");
            }
        }
    }

    private static void WriteNextSteps(string projectName, string projectDirectory, int apiPort)
    {
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), projectDirectory);
        var location = relative.StartsWith("..", StringComparison.Ordinal) ? projectDirectory : relative;

        Console.WriteLine();
        Console.WriteLine($"Created {projectName}.");
        Console.WriteLine();
        Console.WriteLine("Next:");
        Console.WriteLine();
        Console.WriteLine($"  cd {location}");
        Console.WriteLine("  docker compose up -d");
        Console.WriteLine();
        Console.WriteLine($"  API              http://localhost:{apiPort}");
        Console.WriteLine($"  API reference    http://localhost:{apiPort}/scalar/v1");
        Console.WriteLine("  Mail catcher     http://localhost:8025");
        Console.WriteLine("  Kafka console    http://localhost:8085");
        Console.WriteLine();
        Console.WriteLine("Review .env before you deploy anywhere - the seeded administrator password is still the sample one.");
        Console.WriteLine("Docs are in docs/, starting with docs/install.adoc.");
        Console.WriteLine();
    }

    private static void WriteHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Usage: dotnet-api-template new --project-name <name> [options]");
        Console.WriteLine();
        Console.WriteLine("Scaffolds a clean-architecture ASP.NET Core Web API. The project name is applied");
        Console.WriteLine("across the board: the solution, every project, every assembly and every namespace.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Options.WriteTo(Console.Out);
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine();
        Console.WriteLine("  dotnet-api-template new --project-name Acme.Billing");
        Console.WriteLine("  dotnet-api-template new --project-name Acme.Billing --allow-grpc --kafka-topic-prefix billing");
        Console.WriteLine("  dotnet-api-template new -n Acme.Billing -o ~/src -p 6000 -d billingdb");
        Console.WriteLine("  dotnet-api-template new -n Acme.Billing --dry-run");
        Console.WriteLine();
    }
}
