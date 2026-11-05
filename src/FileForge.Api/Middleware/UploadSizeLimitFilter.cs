using FileForge.Application.Errors;
using FileForge.Application.Jobs;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace FileForge.Api.Middleware;

/// <summary>
/// A resource filter runs before model binding, i.e. before ASP.NET Core reads the uploaded form.
/// Without it, a huge upload would be read (and buffered to disk) before our size check sees it.
/// </summary>
internal sealed class UploadSizeLimitFilter(IOptions<ConversionOptions> options) : IResourceFilter
{
    // Room for multipart boundaries, part headers and the targetFormat field.
    internal const long MultipartOverheadBytes = 64 * 1024;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var maxFileSize = options.Value.MaxFileSizeBytes;
        var limit = maxFileSize + MultipartOverheadBytes;
        var httpContext = context.HttpContext;

        // Most clients announce the size: reject before reading a single byte.
        if (httpContext.Request.ContentLength > limit)
        {
            throw new AppException(
                ErrorCodes.FileTooLarge,
                $"The file exceeds the maximum size of {maxFileSize} bytes.");
        }

        // A client that does not announce it is cut off by the server once it sends too much.
        var sizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = limit;
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}

public static class UploadSizeLimitExtensions
{
    /// <summary>
    /// Registers the filter and makes every size limit of the upload path follow
    /// Conversions:MaxFileSizeBytes, so changing that one setting is enough.
    /// </summary>
    public static IServiceCollection AddUploadSizeLimit(this IServiceCollection services)
    {
        services.AddScoped<UploadSizeLimitFilter>();

        // ASP.NET Core's default multipart limit is 128 MB, below our 200 MB: left alone, it would
        // reject bigger uploads with a generic 400 before our own check runs.
        services.AddOptions<FormOptions>()
            .Configure<IOptions<ConversionOptions>>((form, conversions) =>
                form.MultipartBodyLengthLimit = conversions.Value.MaxFileSizeBytes + UploadSizeLimitFilter.MultipartOverheadBytes);

        return services;
    }
}
