namespace DotnetApiTemplate.Cli.Generation;

/// <summary>
/// Initialises the generated project as a git repository with one commit, so the first thing a
/// developer does to it is diffable.
/// </summary>
public static class GitRepository
{
    public static async Task<string?> InitialiseAsync(string projectDirectory, CancellationToken cancellationToken)
    {
        if (Directory.Exists(Path.Combine(projectDirectory, ".git")))
        {
            return "the directory is already a git repository";
        }

        var init = await ProcessRunner.RunAsync("git", ["init", "--quiet"], projectDirectory, cancellationToken: cancellationToken);
        if (!init.Succeeded)
        {
            return init.CombinedOutput is { Length: > 0 } message ? message : "git init failed";
        }

        var add = await ProcessRunner.RunAsync("git", ["add", "."], projectDirectory, cancellationToken: cancellationToken);
        if (!add.Succeeded)
        {
            return add.CombinedOutput;
        }

        var commit = await ProcessRunner.RunAsync(
            "git",
            ["commit", "--quiet", "--message", "chore: scaffold from dotnet-api-template"],
            projectDirectory,
            cancellationToken: cancellationToken);

        // A missing user.email is the usual cause, and is the developer's to fix, not ours.
        return commit.Succeeded ? null : commit.CombinedOutput;
    }
}
