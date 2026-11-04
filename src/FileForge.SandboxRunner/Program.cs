using FileForge.Application.Conversions;
using FileForge.Application.Errors;
using FileForge.Infrastructure;
using FileForge.Infrastructure.Sandbox;

// The only FileForge service with access to Docker. It has no database connection, no public port,
// and one operation: run a supported conversion on files that are already in storage.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConversionSandbox();
builder.Services.AddSingleton<ConversionRegistry>();

var app = builder.Build();

app.MapPost("/convert", async (
    RunnerConvertRequest request,
    IConversionExecutor executor,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        // The route is checked against our own list of converters, the keys against the storage
        // key format: nothing in the request becomes a command or a path as-is.
        await executor.ExecuteAsync(
            new ConversionRoute(request.SourceFormat, request.TargetFormat),
            request.InputStorageKey,
            request.OutputStorageKey,
            cancellationToken);

        return Results.NoContent();
    }
    catch (AppException ex)
    {
        logger.LogWarning("Conversion failed with {ErrorCode}", ex.Code);
        return Results.UnprocessableEntity(new RunnerError(ex.Code));
    }
    catch (ArgumentException)
    {
        // Malformed formats or storage keys.
        return Results.BadRequest();
    }
});

app.MapGet("/health", () => Results.Ok());

app.Run();
