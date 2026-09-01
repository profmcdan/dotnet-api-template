using System.Diagnostics;
using System.Text;

namespace DotnetApiTemplate.Cli.Generation;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;

    public string CombinedOutput =>
        string.Join(Environment.NewLine, new[] { StandardOutput, StandardError }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public static class ProcessRunner
{
    /// <summary>
    /// Runs a child process and captures its output. Used for every `dotnet` and `git` call, so
    /// failures can be reported in this tool's own voice rather than as raw SDK noise.
    /// </summary>
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        bool streamOutput = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            stdout.AppendLine(e.Data);
            if (streamOutput)
            {
                Console.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            stderr.AppendLine(e.Data);
            if (streamOutput)
            {
                Console.Error.WriteLine(e.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not start '{fileName}'. Is it installed and on your PATH?", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(process.ExitCode, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd());
    }

    public static Task<ProcessResult> DotnetAsync(
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        bool streamOutput = false,
        CancellationToken cancellationToken = default) =>
        RunAsync("dotnet", arguments, workingDirectory, streamOutput, cancellationToken);
}
