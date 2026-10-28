using FileForge.Api.Contracts;
using FileForge.Application.Conversions;
using Microsoft.AspNetCore.Mvc;

namespace FileForge.Api.Controllers;

[ApiController]
[Route("api/v1/formats")]
public sealed class FormatsController(ConversionRegistry registry) : ControllerBase
{
    [HttpGet]
    public ActionResult<FormatsResponse> Get()
    {
        var formats = registry.SupportedFormats
            .Select(format => new FormatResponse(format.Source, format.Targets))
            .ToList();

        return Ok(new FormatsResponse(formats));
    }
}
