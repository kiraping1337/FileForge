using System.Diagnostics;
using System.Globalization;
using FileForge.Application.Conversions;
using FileForge.Application.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileForge.Infrastructure.Conversion;

internal sealed class ImageMagickConverter(
    IOptions<ImageMagickOptions> options,
    ILogger<ImageMagickConverter> logger) : IFileConverter
{
    internal const string ContainerUser = "10001";
    internal const string ContainerInputPath = "/in/input";
    internal const string ContainerOutputDirectory = "/out";
    internal const string ResultFileName = "result";

    private const int DockerFailedToStart = 125;
    private const int KilledBySignal = 137; // e.g. the out-of-memory killer

    private static readonly string[] Formats = ["jpg", "png", "webp", "bmp"];

    public string Name => "ImageMagick";

    // Every format to every other format: 4 sources x 3 targets.
    public IReadOnlyCollection<ConversionRoute> SupportedRoutes { get; } =
        Formats
            .SelectMany(source => Formats
                .Where(target => target != source)
                .Select(target => new ConversionRoute(source, target)))
            .ToArray();

    public async Task ConvertAsync(
        ConversionRoute route,
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.Sandbox.Enabled)
        {
            await RunAsync(settings.ExecutablePath, BuildLocalArguments(route, inputPath, outputPath),
                settings.Timeout, containerName: null, cancellationToken);
            return;
        }

        // A fresh empty folder per conversion: the container can write only here, and sees nothing else.
        var workDirectory = Path.Combine(settings.WorkPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);

        try
        {
            var containerName = $"fileforge-im-{Guid.NewGuid():N}";
            var arguments = BuildDockerArguments(settings.Sandbox, route, inputPath, workDirectory, containerName);

            await RunAsync(settings.Sandbox.DockerExecutable, arguments, settings.Timeout, containerName, cancellationToken);

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
    internal static IReadOnlyList<string> BuildDockerArguments(
        SandboxOptions sandbox,
        ConversionRoute route,
        string inputPath,
        string workDirectory,
        string containerName) =>
    [
        "run",
        "--rm",
        "--name", containerName,
        "--network", "none",
        "--read-only",
        "--tmpfs", $"/tmp:rw,noexec,nosuid,size={sandbox.TmpSize}",
        "--memory", sandbox.MemoryLimit,
        "--memory-swap", sandbox.MemoryLimit, // equal to --memory: no swap on top of the limit
        "--cpus", sandbox.CpuLimit,
        "--pids-limit", sandbox.PidsLimit.ToString(CultureInfo.InvariantCulture),
        "--user", ContainerUser,
        "--cap-drop", "ALL",
        "--security-opt", "no-new-privileges",
        "--mount", $"type=bind,source={inputPath},target={ContainerInputPath},readonly",
        "--mount", $"type=bind,source={workDirectory},target={ContainerOutputDirectory}",
        sandbox.Image,
        // The "png:" prefix forces the decoder: ImageMagick must not pick one by sniffing the content.
        $"{route.SourceFormat}:{ContainerInputPath}",
        $"{route.TargetFormat}:{ContainerOutputDirectory}/{ResultFileName}",
    ];

    private static IReadOnlyList<string> BuildLocalArguments(ConversionRoute route, string inputPath, string outputPath) =>
    [
        $"{route.SourceFormat}:{inputPath}",
        $"{route.TargetFormat}:{outputPath}",
    ];

    private async Task RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        string? containerName,
        CancellationToken cancellationToken)
    {
        // No shell: arguments go to the program as separate strings, so nothing in them can be
        // interpreted as a command. They contain only server-made paths and formats from our list.
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{fileName} could not be started.");

        try
        {
            // Read stderr while waiting: if its buffer fills up, the process blocks forever.
            var errorOutput = process.StandardError.ReadToEndAsync(timeoutSource.Token);
            await process.WaitForExitAsync(timeoutSource.Token);
            var stderr = await errorOutput;

            if (process.ExitCode != 0)
            {
                HandleFailure(process.ExitCode, stderr, containerName is not null);
            }
        }
        catch (OperationCanceledException)
        {
            Kill(process);

            // Killing the docker CLI does not stop the container: stop it explicitly.
            if (containerName is not null)
            {
                await KillContainerAsync(containerName);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw; // the worker is shutting down, not a timeout
            }

            throw new AppException(ErrorCodes.ProcessingTimeout, "The conversion took too long.");
        }
    }

    private void HandleFailure(int exitCode, string stderr, bool inDocker)
    {
        // stderr only at Debug level: it may contain paths and details about the user's file.
        logger.LogDebug("Converter error output: {ErrorOutput}", stderr);

        if (inDocker && exitCode == DockerFailedToStart)
        {
            // Our problem, not the user's file: image not built, Docker not running, mount failed.
            logger.LogError("Docker could not start the converter container. Is the image built and Docker running?");
            throw new InvalidOperationException("The converter container could not be started.");
        }

        if (inDocker && exitCode == KilledBySignal)
        {
            logger.LogWarning("Converter container was killed, most likely by the memory limit");
        }
        else
        {
            logger.LogWarning("ImageMagick exited with code {ExitCode}", exitCode);
        }

        throw new AppException(ErrorCodes.ProcessingFailed, "The file could not be converted.");
    }

    private async Task KillContainerAsync(string containerName)
    {
        try
        {
            var startInfo = new ProcessStartInfo(options.Value.Sandbox.DockerExecutable)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
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
