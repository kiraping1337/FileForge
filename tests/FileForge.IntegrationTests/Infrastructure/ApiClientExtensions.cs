using System.Net.Http.Headers;
using System.Net.Http.Json;
using FileForge.Api.Contracts;

namespace FileForge.IntegrationTests.Infrastructure;

/// <summary>Short helpers for the calls almost every test makes.</summary>
public static class ApiClientExtensions
{
    public const string TokenHeader = "X-Job-Token";

    /// <summary>POST /conversions as multipart/form-data, exactly like the frontend sends it.</summary>
    public static Task<HttpResponseMessage> PostConversionAsync(
        this HttpClient client,
        byte[] content,
        string fileName,
        string targetFormat)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(targetFormat), "targetFormat");

        return client.PostAsync("/api/v1/conversions", form);
    }

    /// <summary>Creates a job and fails the test right away if that did not work.</summary>
    public static async Task<CreateConversionResponse> CreateJobAsync(
        this HttpClient client,
        string targetFormat = "jpg")
    {
        var response = await client.PostConversionAsync(TestFiles.Png, "photo.png", targetFormat);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreateConversionResponse>())!;
    }

    /// <summary>A request to /conversions/{jobId}{suffix} with the token header (if a token is given).</summary>
    public static Task<HttpResponseMessage> SendWithTokenAsync(
        this HttpClient client,
        HttpMethod method,
        Guid jobId,
        string? token,
        string suffix = "")
    {
        var request = new HttpRequestMessage(method, $"/api/v1/conversions/{jobId}{suffix}");
        if (token is not null)
        {
            request.Headers.Add(TokenHeader, token);
        }

        return client.SendAsync(request);
    }
}
