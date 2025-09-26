using LibGit2Sharp;
using Spectre.Console;
using System.Diagnostics;

namespace GitCloner;

public class Program
{
    private static readonly string DefaultRepoListFile = "repos.txt";
    private static readonly string DefaultCloneDirectory = "cloned-repos";
    
    public static async Task Main(string[] args)
    {
        var repoListFile = args.Length > 0 ? args[0] : DefaultRepoListFile;
        var cloneDirectory = args.Length > 1 ? args[1] : DefaultCloneDirectory;
        
        AnsiConsole.Write(
            new FigletText("Git Cloner")
                .LeftJustified()
                .Color(Color.Blue));
        
        if (!File.Exists(repoListFile))
        {
            AnsiConsole.MarkupLine($"[red]Error: Repository list file '{repoListFile}' not found![/]");
            AnsiConsole.MarkupLine($"[yellow]Please create a '{repoListFile}' file with one repository URL per line.[/]");
            AnsiConsole.MarkupLine($"[dim]Example:[/]");
            AnsiConsole.MarkupLine($"[dim]https://github.com/user/repo1[/]");
            AnsiConsole.MarkupLine($"[dim]https://github.com/user/repo2[/]");
            return;
        }
        
        // Ensure clone directory exists
        Directory.CreateDirectory(cloneDirectory);
        
        var repoUrls = await File.ReadAllLinesAsync(repoListFile);
        var results = new List<RepoResult>();
        
        AnsiConsole.MarkupLine($"[green]Found {repoUrls.Length} repositories to process[/]");
        AnsiConsole.WriteLine();
        
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Cloning repositories...[/]");
                task.MaxValue = repoUrls.Length;
                
                foreach (var repoUrl in repoUrls)
                {
                    if (string.IsNullOrWhiteSpace(repoUrl) || repoUrl.StartsWith("#"))
                    {
                        task.Increment(1);
                        continue;
                    }
                    
                    var result = await ProcessRepository(repoUrl.Trim(), cloneDirectory);
                    results.Add(result);
                    task.Increment(1);
                }
            });
        
        DisplayResults(results);
    }
    
    private static Task<RepoResult> ProcessRepository(string repoUrl, string baseCloneDirectory)
    {
        try
        {
            var repoInfo = ParseRepoUrl(repoUrl);
            if (repoInfo == null)
            {
                return Task.FromResult(new RepoResult("Unknown", "Unknown", "Invalid URL", DateTime.Now, false));
            }
            
            var repoPath = Path.Combine(baseCloneDirectory, repoInfo.RepoName);
            var isNewRepo = !Directory.Exists(repoPath);
            var hasNewChanges = false;
            
            if (isNewRepo)
            {
                // Clone new repository
                Repository.Clone(repoUrl, repoPath);
                hasNewChanges = true;
                
                // Set execute permissions on .sh files for Unix systems
                SetExecutePermissionsOnShellScripts(repoPath);
            }
            else
            {
                // Pull existing repository
                using var repo = new Repository(repoPath);
                var remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                
                // Get current commit before fetch
                var beforeCommit = repo.Head.Tip?.Sha;
                
                Commands.Fetch(repo, remote.Name, refSpecs, null, "");
                
                // Check if we have new commits
                var originMain = repo.Branches["origin/main"] ?? repo.Branches["origin/master"];
                if (originMain != null)
                {
                    var afterCommit = originMain.Tip?.Sha;
                    hasNewChanges = beforeCommit != afterCommit;
                    
                    if (hasNewChanges)
                    {
                        // Fast-forward merge if possible
                        try
                        {
                            Commands.Checkout(repo, originMain, new CheckoutOptions());
                            repo.Reset(ResetMode.Hard, originMain.Tip);
                            
                            // Set execute permissions on .sh files for Unix systems after update
                            SetExecutePermissionsOnShellScripts(repoPath);
                        }
                        catch
                        {
                            // If fast-forward fails, just note that there are changes
                        }
                    }
                }
            }
            
            var status = isNewRepo ? "New" : (hasNewChanges ? "Updated" : "Existing");
            return Task.FromResult(new RepoResult(repoInfo.UserName, repoInfo.RepoName, status, DateTime.Now, hasNewChanges));
        }
        catch (Exception ex)
        {
            var repoInfo = ParseRepoUrl(repoUrl) ?? new RepoInfo("Unknown", "Unknown");
            return Task.FromResult(new RepoResult(repoInfo.UserName, repoInfo.RepoName, $"Error: {ex.Message}", DateTime.Now, false));
        }
    }
    
    private static RepoInfo? ParseRepoUrl(string repoUrl)
    {
        try
        {
            // Handle both HTTPS and SSH URLs
            if (repoUrl.StartsWith("https://github.com/"))
            {
                var parts = repoUrl.Replace("https://github.com/", "").TrimEnd('/').Split('/');
                if (parts.Length >= 2)
                {
                    return new RepoInfo(parts[0], parts[1].Replace(".git", ""));
                }
            }
            else if (repoUrl.StartsWith("git@github.com:"))
            {
                var parts = repoUrl.Replace("git@github.com:", "").TrimEnd('/').Split('/');
                if (parts.Length >= 2)
                {
                    return new RepoInfo(parts[0], parts[1].Replace(".git", ""));
                }
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }
    
    private static void SetExecutePermissionsOnShellScripts(string repoPath)
    {
        // Only set permissions on Unix systems (macOS and Linux)
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                // Find all .sh files recursively
                var shellScripts = Directory.GetFiles(repoPath, "*.sh", SearchOption.AllDirectories);
                
                foreach (var scriptPath in shellScripts)
                {
                    // Use chmod to set execute permissions (755 = rwxr-xr-x)
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"+x \"{scriptPath}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    process.Start();
                    process.WaitForExit();
                    
                    if (process.ExitCode != 0)
                    {
                        var error = process.StandardError.ReadToEnd();
                        AnsiConsole.MarkupLine($"[yellow]Warning: Failed to set permissions on {scriptPath}: {error}[/]");
                    }
                }
                
                if (shellScripts.Length > 0)
                {
                    AnsiConsole.MarkupLine($"[dim]Set execute permissions on {shellScripts.Length} shell script(s) in {Path.GetFileName(repoPath)}[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Failed to set shell script permissions: {ex.Message}[/]");
            }
        }
    }
    
    private static void DisplayResults(List<RepoResult> results)
    {
        AnsiConsole.WriteLine();
        
        var table = new Table();
        table.AddColumn("UserName");
        table.AddColumn("RepoName");
        table.AddColumn("Status");
        table.AddColumn("LastUpdatedTime");
        
        foreach (var result in results)
        {
            var timeStr = result.LastUpdatedTime.ToString("yyyy-MM-dd HH:mm:ss");
            
            if (result.HasNewChanges)
            {
                table.AddRow(
                    $"[green]{result.UserName}[/]",
                    $"[green]{result.RepoName}[/]",
                    $"[green]{result.Status}[/]",
                    $"[green]{timeStr}[/]"
                );
            }
            else
            {
                var statusColor = result.Status.StartsWith("Error") ? "red" : "white";
                table.AddRow(
                    result.UserName,
                    result.RepoName,
                    $"[{statusColor}]{result.Status}[/]",
                    timeStr
                );
            }
        }
        
        AnsiConsole.Write(table);
        
        // Summary
        var newRepos = results.Count(r => r.Status == "New");
        var updatedRepos = results.Count(r => r.Status == "Updated");
        var existingRepos = results.Count(r => r.Status == "Existing");
        var errorRepos = results.Count(r => r.Status.StartsWith("Error"));
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Summary:[/]");
        AnsiConsole.MarkupLine($"[green]New repositories:[/] {newRepos}");
        AnsiConsole.MarkupLine($"[yellow]Updated repositories:[/] {updatedRepos}");
        AnsiConsole.MarkupLine($"[blue]Existing repositories:[/] {existingRepos}");
        if (errorRepos > 0)
        {
            AnsiConsole.MarkupLine($"[red]Failed repositories:[/] {errorRepos}");
        }
    }
}

public record RepoInfo(string UserName, string RepoName);
public record RepoResult(string UserName, string RepoName, string Status, DateTime LastUpdatedTime, bool HasNewChanges);