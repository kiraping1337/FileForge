using System.Net;
using System.Net.Http.Json;
using FileForge.Application.Conversions;
using FileForge.Application.Errors;

namespace FileForge.Infrastructure.Sandbox;

/// <summary>
/// Used by the worker in Docker: asks the sandbox runner to do the conversion.
/// The worker itself never touches Docker. Both share the storage volume, so only keys travel.
/// Cancelling the token aborts the HTTP request; the runner then kills the converter container.
/// </summary>
internal sealed class RemoteConversionExecutor(HttpClient http) : IConversionExecutor
{
    public async Task ExecuteAsync(
        ConversionRoute route,
        string inputStorageKey,
        string outputStorageKey,
        CancellationToken cancellationToken)
    {
        var request = new RunnerConvertRequest(route.SourceFormat, route.TargetFormat, inputStorageKey, outputStorageKey);

        using var response = await http.PostAsJsonAsync("convert", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var error = await response.Content.ReadFromJsonAsync<RunnerError>(cancellationToken);
            throw new AppException(error?.Code ?? ErrorCodes.ProcessingFailed, "The file could not be converted.");
        }

        // Anything else is our problem (runner down, bad request), not the user's file.
        throw new InvalidOperationException($"The sandbox runner answered {(int)response.StatusCode}.");
    }
}
