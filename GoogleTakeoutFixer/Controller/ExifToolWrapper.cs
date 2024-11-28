using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.Exceptions;

namespace GoogleTakeoutFixer.Controller;

public class ExifToolWrapper
{
    private readonly bool _isLinux = Environment.OSVersion.Platform == PlatformID.Unix;

    public string ExifToolPath { get; private set; } = string.Empty;

    public async Task DetectExifTool()
    {
        if (!string.IsNullOrWhiteSpace(ExifToolPath))
        {
            return;
        }
        
        if (!_isLinux)
        {
            throw new PlatformNotSupportedException($"Only Linux platforms are supported.");
        }

        var result = await RunExecutableAsync("which", ["exiftool"]);
        if (result.ExitCode == 0)
        {
            ExifToolPath = result.StandardOutput.Trim('\n');
        }

        if (string.IsNullOrWhiteSpace(ExifToolPath))
        {
            throw new InvalidOperationException($"Cannot find exiftool. Please ensure that the exiftool is installed.");
        }
    }

    public CommandExecutionResult RunExifTool(string[] arguments)
    {
        return RunExecutable(ExifToolPath, arguments);
    }

    public async Task< CommandExecutionResult> RunExifToolAsync(string[] arguments)
    {
        return await RunExecutableAsync(ExifToolPath, arguments);
    }

    private CommandExecutionResult RunExecutable(string executable, string[] arguments)
    {
        var result = Task.Run(async () => await RunExecutableAsync(executable, arguments)).Result;
        return result;
    }

    private async Task<CommandExecutionResult> RunExecutableAsync(string executable, string[] arguments)
    {
        try
        {
            // todo: Pass working path
            var result = await Cli.Wrap(executable)
                .WithArguments(arguments)
                // .WithWorkingDirectory("work/dir/path")
                // This can be simplified with `ExecuteBufferedAsync()`
                .ExecuteBufferedAsync();
            return new CommandExecutionResult()
            {
                ExitCode = result.ExitCode,
                StandardOutput = result.StandardOutput.Trim('\n', ' '),
                StandardError = result.StandardError.Trim('\n', ' '),
                ExecutionTime = result.ExitTime - result.StartTime,
            };
        }
        catch (CommandExecutionException error)
        {
            return new CommandExecutionResult()
            {
                ExitCode = error.ExitCode,
                StandardError = error.Message.Trim('\n'),
            };
        }
    }
}

public class CommandExecutionResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; } = TimeSpan.Zero;
}