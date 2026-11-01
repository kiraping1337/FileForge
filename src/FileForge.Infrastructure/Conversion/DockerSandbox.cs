using System.Diagnostics;
using System.Globalization;
using FileForge.Application.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileForge.Infrastructure.Conversion;

/// <summary>
/// Runs a converter in a one-off container. The container sees only the input file (read-only)
/// and an empty output folder; everything else about the sandbox is in BuildRunArguments.
/// </summary>
internal sealed class DockerSandbox(IOptions<SandboxOptions> options, ILogger<DockerSandbox> logger)
{
    internal const string ContainerUser = "10001";
    internal const string InputPath = "/in/input";
    internal const string OutputDirectory = "/out";
    internal const string ResultFileName = "result";
    internal const string OutputPath = OutputDirectory + "/" + ResultFileName;

    private const int DockerFailedToStart = 125;
    private const int KilledBySignal = 137; // e.g. the out-of-memory killer

    /// <param name="toolArguments">Arguments for the tool inside the image (magick, ffmpeg).
    /// They must read <see cref="InputPath"/> and write <see cref="OutputPath"/>.</param>
    public async Task RunAsync(
        ConverterContainerOptions container,
        IReadOnlyList<string> toolArguments,
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // A fresh empty folder per conversion: the container can write only here.
        var workDirectory = Path.Combine(settings.WorkPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);

        try
        {
            var containerName = $"fileforge-{Guid.NewGuid():N}";
            var arguments = BuildRunArguments(settings, container, inputPath, workDirectory, containerName, toolArguments);

            await RunProcessAsync(arguments, container.Timeout, containerName, cancellationToken);

            File.Move(Path.Combine(workDirectory, ResultFileName), outputPath);
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    /// <summary>
    /// The full "docker run" command. Every restriction of the sandbox is here, in one place,
    /// so tests can check that none of them is lost.
    /// </summary>
    internal static IReadOnlyList<string> BuildRunArguments(
        SandboxOptions sandbox,
        ConverterContainerOptions container,
        string inputPath,
        string workDirectory,
        string containerName,
        IReadOnlyList<string> toolArguments) =>
    [
        "run",
        "--rm",
        "--name", containerName,
        "--network", "none",
        "--read-only",
        "--tmpfs", $"/tmp:rw,noexec,nosuid,size={sandbox.TmpSize}",
        "--memory", container.MemoryLimit,
        "--memory-swap", container.MemoryLimit, // equal to --memory: no swap on top of the limit
        "--cpus", container.CpuLimit,
        "--pids-limit", sandbox.PidsLimit.ToString(CultureInfo.InvariantCulture),
        "--user", ContainerUser,
        "--cap-drop", "ALL",
        "--security-opt", "no-new-privileges",
        "--mount", $"type=bind,source={inputPath},target={InputPath},readonly",
        "--mount", $"type=bind,source={workDirectory},target={OutputDirectory}",
        container.Image,
        .. toolArguments,
    ];

    private async Task RunProcessAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        string containerName,
        CancellationToken cancellationToken)
    {
        // No shell: arguments go to the program as separate strings, so nothing in them can be
        // interpreted as a command. They contain only server-made paths and values from our own lists.
        var startInfo = CreateStartInfo();
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Docker could not be started.");

        try
        {
            // Read stderr while waiting: if its buffer fills up, the process blocks forever.
            var errorOutput = process.StandardError.ReadToEndAsync(timeoutSource.Token);
            await process.WaitForExitAsync(timeoutSource.Token);
            var stderr = await errorOutput;

            if (process.ExitCode != 0)
            {
                HandleFailure(process.ExitCode, stderr);
            }
        }
        catch (OperationCanceledException)
        {
            Kill(process);

            // Killing the docker CLI does not stop the container: stop it explicitly.
            await KillContainerAsync(containerName);

            if (cancellationToken.IsCancellationRequested)
            {
                throw; // the worker is shutting down, not a timeout
            }

            throw new AppException(ErrorCodes.ProcessingTimeout, "The conversion took too long.");
        }
    }

    private void HandleFailure(int exitCode, string stderr)
    {
        // stderr only at Debug level: it may contain paths and details about the user's file.
        logger.LogDebug("Converter error output: {ErrorOutput}", stderr);

        if (exitCode == DockerFailedToStart)
        {
            // Our problem, not the user's file: image not built, Docker not running, mount failed.
            logger.LogError("Docker could not start the converter container. Is the image built and Docker running?");
            throw new InvalidOperationException("The converter container could not be started.");
        }

        if (exitCode == KilledBySignal)
        {
            logger.LogWarning("Converter container was killed, most likely by the memory limit");
        }
        else
        {
            logger.LogWarning("Converter exited with code {ExitCode}", exitCode);
        }

        throw new AppException(ErrorCodes.ProcessingFailed, "The file could not be converted.");
    }

    private async Task KillContainerAsync(string containerName)
    {
        try
        {
            var startInfo = CreateStartInfo();
            startInfo.ArgumentList.Add("kill");
            startInfo.ArgumentList.Add(containerName);

            using var kill = Process.Start(startInfo);
            if (kill is not null)
            {
                using var waitLimit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await kill.WaitForExitAsync(waitLimit.Token);
            }
        }
        catch (Exception ex)
        {
            // Best effort: --rm removes the container as soon as it stops by itself anyway.
            logger.LogWarning("Could not kill converter container ({ErrorType})", ex.GetType().Name);
        }
    }

    private ProcessStartInfo CreateStartInfo() => new(options.Value.DockerExecutable)
    {
        UseShellExecute = false,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Could not delete a converter work folder ({ErrorType})", ex.GetType().Name);
        }
    }
}
