using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SVNFileManager.Models;

namespace SVNFileManager.Services;

public class SvnService
{
    private readonly string _svnPath;

    public SvnService()
    {
        _svnPath = FindSvnPath();
    }

    private string FindSvnPath()
    {
        var path = FindOnPath("svn");
        if (!string.IsNullOrEmpty(path)) return path;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var possiblePaths = new[]
        {
            Path.Combine(programFiles, "TortoiseSVN", "bin", "svn.exe"),
            Path.Combine(programFiles, "VisualSVN Server", "bin", "svn.exe"),
            Path.Combine(programFiles, "SlikSvn", "bin", "svn.exe"),
        };

        foreach (var p in possiblePaths)
        {
            if (File.Exists(p)) return p;
        }

        return "svn";
    }

    private string? FindOnPath(string command)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
            if (process?.ExitCode == 0) return command;
        }
        catch { }
        return null;
    }

    public async Task<bool> IsSvnAvailableAsync()
    {
        try
        {
            var result = await RunCommandAsync("--version --quiet");
            return !string.IsNullOrEmpty(result.output) && result.exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Dictionary<string, SvnStatus>> GetStatusAsync(string workingCopyPath)
    {
        var statuses = new Dictionary<string, SvnStatus>();

        try
        {
            var result = await RunCommandAsync($"status --non-interactive \"{workingCopyPath}\"");

            if (result.exitCode != 0)
            {
                Console.WriteLine($"SVN status error: {result.error}");
                return statuses;
            }

            var lines = result.output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Length < 8) continue;

                var statusChar = line[0];
                var path = line.Substring(8).Trim();

                var status = statusChar switch
                {
                    'M' => SvnStatus.Modified,
                    'A' => SvnStatus.Added,
                    'D' => SvnStatus.Deleted,
                    'C' => SvnStatus.Conflicted,
                    '?' => SvnStatus.Unversioned,
                    '!' => SvnStatus.Missing,
                    'R' => SvnStatus.Replaced,
                    '~' => SvnStatus.Obstructed,
                    'X' => SvnStatus.External,
                    'I' => SvnStatus.Unknown,
                    _ => SvnStatus.Normal
                };

                if (status != SvnStatus.Normal || !statuses.ContainsKey(path))
                {
                    statuses[path] = status;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting SVN status: {ex.Message}");
        }

        return statuses;
    }

    public async Task<string> GetRepoUrlAsync(string workingCopyPath)
    {
        try
        {
            var result = await RunCommandAsync($"info --non-interactive \"{workingCopyPath}\"");
            if (result.exitCode == 0)
            {
                var match = Regex.Match(result.output, @"^URL:\s*(.+)$", RegexOptions.Multiline);
                if (match.Success) return match.Groups[1].Value.Trim();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting repo URL: {ex.Message}");
        }
        return "";
    }

    public async Task<(string output, int exitCode, string error)> RunCommandAsync(string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = _svnPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var output = "";
            var error = "";

            process.OutputDataReceived += (_, e) => output += e.Data + "\n";
            process.ErrorDataReceived += (_, e) => error += e.Data + "\n";

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            return (output.Trim(), process.ExitCode, error.Trim());
        }
        catch (Exception ex)
        {
            return ("", 1, ex.Message);
        }
    }

    public async Task<bool> CommitAsync(string workingCopyPath, string message)
    {
        var result = await RunCommandAsync($"commit --non-interactive -m \"{message}\" \"{workingCopyPath}\"");
        return result.exitCode == 0;
    }

    public async Task<bool> UpdateAsync(string workingCopyPath)
    {
        var result = await RunCommandAsync($"update --non-interactive \"{workingCopyPath}\"");
        return result.exitCode == 0;
    }

    public async Task<bool> AddFileAsync(string filePath)
    {
        var result = await RunCommandAsync($"add --non-interactive \"{filePath}\"");
        return result.exitCode == 0;
    }

    public async Task<bool> RevertAsync(string path, bool recursive = true)
    {
        var recurseFlag = recursive ? "--recursive" : "";
        var result = await RunCommandAsync($"revert --non-interactive {recurseFlag} \"{path}\"");
        return result.exitCode == 0;
    }

    public async Task<bool> CleanupAsync(string workingCopyPath)
    {
        var result = await RunCommandAsync($"cleanup \"{workingCopyPath}\"");
        return result.exitCode == 0;
    }

    public async Task<(string output, int exitCode)> SvnAddRecursiveAsync(string directoryPath)
    {
        var result = await RunCommandAsync($"add --force --non-interactive \"{directoryPath}\"");
        return (result.output, result.exitCode);
    }

    /// <summary>
    /// Checkout a remote SVN repository to a local path.
    /// </summary>
    /// <param name="url">Remote repository URL</param>
    /// <param name="localPath">Local checkout destination</param>
    /// <param name="username">SVN username (optional)</param>
    /// <param name="password">SVN password (optional)</param>
    /// <returns>(output, exitCode)</returns>
    public async Task<(string output, int exitCode)> CheckoutAsync(
        string url,
        string localPath,
        string? username = null,
        string? password = null)
    {
        var args = $"checkout --non-interactive \"{url}\" \"{localPath}\"";
        if (!string.IsNullOrEmpty(username))
        {
            args = $"--username \"{username}\" " + args;
            // NOTE: password is passed via --passwordcmd or prompt is shown by svn
            // For interactive password, we rely on svn caching credentials in %APPDATA%\Subversion\auth
            if (!string.IsNullOrEmpty(password))
            {
                args = $"--password \"{password}\" " + args;
            }
        }

        var result = await RunCommandAsync(args);
        return (result.output + (string.IsNullOrEmpty(result.error) ? "" : $"\nError: {result.error}"), result.exitCode);
    }
}
