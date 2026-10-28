using System.ComponentModel.DataAnnotations;

namespace FileForge.Api.Contracts;

/// <summary>multipart/form-data fields: "file" and "targetFormat".</summary>
public sealed class CreateConversionRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    [MaxLength(16)]
    public string TargetFormat { get; set; } = null!;
}
