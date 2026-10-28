using FileForge.Api.Contracts;
using FileForge.Application.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace FileForge.Api.Controllers;

[ApiController]
[Route("api/v1/conversions")]
public sealed class ConversionsController(CreateConversionService createConversion,
                                          GetConversionService getConversion) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<CreateConversionResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromForm] CreateConversionRequest request,
        CancellationToken cancellationToken)
    {
        await using var content = request.File.OpenReadStream();

        var created = await createConversion.CreateAsync(
            content, request.File.FileName, request.TargetFormat, cancellationToken);

        var statusUrl = $"/api/v1/conversions/{created.JobId}";

        // The body contains a secret: no browser or proxy cache may keep it.
        Response.Headers.CacheControl = "no-store";

        return Accepted(statusUrl, new CreateConversionResponse(
            created.JobId, created.AccessToken, created.Status.ToString(), statusUrl));
    }
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetConversionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetConversionResponse>> Get(
        Guid id,
        [FromHeader(Name = "X-Job-Token")] string? token,
        CancellationToken cancellationToken)
    {
        var job = await getConversion.GetAsync(id, token, cancellationToken);
        return Ok(new GetConversionResponse(job.JobId, job.OriginalFileName, job.SourceFormat, job.TargetFormat,
                                        job.Status, job.OutputSizeBytes, job.ErrorCode, job.CreatedAtUtc, job.ExpiresAtUtc, job.DownloadAvailable));
    }
}
