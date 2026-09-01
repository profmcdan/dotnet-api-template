using DotnetApiTemplate.Cli.CommandLine;
using DotnetApiTemplate.Cli.Commands;
using DotnetApiTemplate.Cli.Generation;

using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

try
{
    return await RunAsync(args, cancellation.Token);
}
catch (CommandLineException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ExitCodes.UsageError;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return ExitCodes.Cancelled;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return ExitCodes.GenerationFailed;
}

static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
{
    if (args.Length == 0)
    {
        WriteRootHelp();
        return ExitCodes.Success;
    }

    var command = args[0];
    var rest = args.Skip(1).ToArray();

    return command switch
    {
        NewCommand.Name or "create" or "scaffold" => await NewCommand.RunAsync(rest, cancellationToken),
        "update" => await MaintenanceCommands.UpdateAsync(cancellationToken),
        "uninstall-template" => await MaintenanceCommands.UninstallTemplateAsync(cancellationToken),
        "info" or "doctor" => await MaintenanceCommands.InfoAsync(cancellationToken),
        "--version" or "-v" or "version" => WriteVersion(),
        "--help" or "-h" or "help" => WriteRootHelpAndSucceed(),
        _ => UnknownCommand(command),
    };
}

static int WriteVersion()
{
    Console.WriteLine(TemplatePackage.Version);
    return ExitCodes.Success;
}

static int WriteRootHelpAndSucceed()
{
    WriteRootHelp();
    return ExitCodes.Success;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    Console.Error.WriteLine("Run 'dotnet-api-template --help' to see what is available.");
    return ExitCodes.UsageError;
}

static void WriteRootHelp()
{
    Console.WriteLine();
    Console.WriteLine("dotnet-api-template - scaffold a clean-architecture ASP.NET Core Web API");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet-api-template <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  new                 Create a new project");
    Console.WriteLine("  info                Show the tool version and check for the SDK, Docker and git");
    Console.WriteLine("  update              Re-register the bundled template with the .NET SDK");
    Console.WriteLine("  uninstall-template  Unregister the template, leaving the tool installed");
    Console.WriteLine("  version             Print the version");
    Console.WriteLine();
    Console.WriteLine("Run 'dotnet-api-template new --help' for the full set of generation options.");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine();
    Console.WriteLine("  dotnet-api-template new --project-name Acme.Billing --allow-grpc --kafka-topic-prefix billing");
    Console.WriteLine();
}
