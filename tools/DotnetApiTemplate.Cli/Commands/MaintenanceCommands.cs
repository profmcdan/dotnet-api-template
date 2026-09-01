using DotnetApiTemplate.Cli.CommandLine;
using DotnetApiTemplate.Cli.Generation;

namespace DotnetApiTemplate.Cli.Commands;

/// <summary>
/// Housekeeping around the bundled template: re-register it, remove it, or report what is
/// installed. Useful when the SDK's template cache and this tool disagree.
/// </summary>
public static class MaintenanceCommands
{
    public static async Task<int> UpdateAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Re-registering the bundled template with the .NET SDK…");
        await TemplatePackage.EnsureInstalledAsync(force: true, cancellationToken);

        Console.WriteLine($"Template version {TemplatePackage.Version} is registered as '{TemplatePackage.ShortName}'.");
        Console.WriteLine();
        Console.WriteLine("To pick up a newer template, update the tool itself:");
        Console.WriteLine("  dotnet tool update --global DotnetApiTemplate.Cli");
        Console.WriteLine();
        return ExitCodes.Success;
    }

    public static async Task<int> UninstallTemplateAsync(CancellationToken cancellationToken)
    {
        var removed = await TemplatePackage.UninstallAsync(cancellationToken);

        Console.WriteLine(removed
            ? "Unregistered the template from the .NET SDK. The tool itself is still installed."
            : "The template was not registered with the SDK; nothing to do.");

        Console.WriteLine();
        Console.WriteLine("To remove the tool as well:");
        Console.WriteLine("  dotnet tool uninstall --global DotnetApiTemplate.Cli");
        Console.WriteLine();
        return ExitCodes.Success;
    }

    public static async Task<int> InfoAsync(CancellationToken cancellationToken)
    {
        var sdk = await ProcessRunner.DotnetAsync(["--version"], cancellationToken: cancellationToken);
        var docker = await SafeVersionAsync("docker", ["--version"], cancellationToken);
        var git = await SafeVersionAsync("git", ["--version"], cancellationToken);

        Console.WriteLine();
        Console.WriteLine($"  dotnet-api-template  {TemplatePackage.Version}");
        Console.WriteLine($"  template short name  {TemplatePackage.ShortName}");
        Console.WriteLine($"  template location    {TemplatePackage.Root}");
        Console.WriteLine($"  template present     {(TemplatePackage.Exists ? "yes" : "no")}");
        Console.WriteLine();
        Console.WriteLine($"  .NET SDK             {(sdk.Succeeded ? sdk.StandardOutput.Trim() : "not found")}");
        Console.WriteLine($"  Docker               {docker ?? "not found - needed to run a generated project"}");
        Console.WriteLine($"  git                  {git ?? "not found - --no-git will be used"}");
        Console.WriteLine();

        if (!TemplatePackage.Exists)
        {
            Console.Error.WriteLine("The bundled template is missing. Reinstall with:");
            Console.Error.WriteLine("  dotnet tool update --global DotnetApiTemplate.Cli");
            return ExitCodes.GenerationFailed;
        }

        return ExitCodes.Success;
    }

    private static async Task<string?> SafeVersionAsync(string fileName, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(fileName, args, cancellationToken: cancellationToken);
            return result.Succeeded ? result.StandardOutput.Trim() : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
