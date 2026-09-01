using System.Reflection;

namespace DotnetApiTemplate.Cli.Generation;

/// <summary>
/// Locates the template that ships inside this tool and keeps it registered with the .NET SDK.
/// <para>
/// The template travels with the tool so that <c>dotnet tool install</c> is the only step a user
/// has to take. Registration is cached against the tool version, so the one-second
/// <c>dotnet new install</c> is paid once per upgrade rather than on every command.
/// </para>
/// </summary>
public sealed class TemplatePackage
{
    public const string ShortName = "cleanapi";

    private static readonly string StateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet-api-template");

    private static readonly string VersionMarker = Path.Combine(StateDirectory, "installed-template-version");

    public static string Version =>
        typeof(TemplatePackage).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(TemplatePackage).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>The template root shipped alongside the tool binary.</summary>
    public static string Root => Path.Combine(AppContext.BaseDirectory, "template");

    public static bool Exists => File.Exists(Path.Combine(Root, ".template.config", "template.json"));

    /// <summary>
    /// Registers the template with the SDK if this tool version has not already done so.
    /// Pass <paramref name="force"/> to re-register regardless.
    /// </summary>
    public static async Task<bool> EnsureInstalledAsync(bool force, CancellationToken cancellationToken)
    {
        if (!Exists)
        {
            throw new InvalidOperationException(
                $"The bundled template is missing from '{Root}'. Reinstall the tool with "
                + "'dotnet tool update --global ProfmcdanDotnetApiTemplate.Cli'.");
        }

        if (!force && IsCurrentVersionRegistered())
        {
            return false;
        }

        var result = await ProcessRunner.DotnetAsync(
            ["new", "install", Root, "--force"], cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Registering the template failed.{Environment.NewLine}{result.CombinedOutput}");
        }

        MarkRegistered();
        return true;
    }

    public static async Task<bool> UninstallAsync(CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.DotnetAsync(["new", "uninstall", Root], cancellationToken: cancellationToken);

        if (File.Exists(VersionMarker))
        {
            File.Delete(VersionMarker);
        }

        return result.Succeeded;
    }

    private static bool IsCurrentVersionRegistered()
    {
        try
        {
            return File.Exists(VersionMarker)
                && string.Equals(File.ReadAllText(VersionMarker).Trim(), Version, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void MarkRegistered()
    {
        try
        {
            Directory.CreateDirectory(StateDirectory);
            File.WriteAllText(VersionMarker, Version);
        }
        catch (IOException)
        {
            // A missing marker only costs a second on the next run; never fail generation for it.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
