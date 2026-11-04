using FileForge.Api.Contracts;
using FileForge.Application.Errors;
using FileForge.Domain.Enums;
using FileForge.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FileForge.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class JobLifecycleTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetStatus_TokenOfAnotherJob_Returns403()
    {
        var job = await _client.CreateJobAsync();
        var otherJob = await _client.CreateJobAsync();

        var response = await _client.SendWithTokenAsync(HttpMethod.Get, job.JobId, otherJob.AccessToken);

        await AssertAccessDeniedAsync(response);
    }

    [Fact]
    public async Task GetStatus_NoToken_Returns403()
    {
        var job = await _client.CreateJobAsync();

        var response = await _client.SendWithTokenAsync(HttpMethod.Get, job.JobId, null);

        await AssertAccessDeniedAsync(response);
    }

    [Fact]
    public async Task GetStatus_UnknownJob_Returns403()
    {
        var job = await _client.CreateJobAsync();

        var response = await _client.SendWithTokenAsync(HttpMethod.Get, Guid.NewGuid(), job.AccessToken);

        await AssertAccessDeniedAsync(response);
    }

    [Fact]
    public async Task Download_CompletedJob_ReturnsConvertedFile()
    {
        var created = await _client.CreateJobAsync();

        var processed = await factory.RunWorkerOnceAsync();
        Assert.True(processed);

        var response = await _client.SendWithTokenAsync(HttpMethod.Get, created.JobId, created.AccessToken, "/download");
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestFiles.Png, bytes);
        Assert.Equal("photo.jpg", fileName);
    }

    [Fact]
    public async Task Download_PendingJob_Returns409()
    {
        var created = await _client.CreateJobAsync();

        var response = await _client.SendWithTokenAsync(HttpMethod.Get, created.JobId, created.AccessToken, "/download");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("JOB_NOT_COMPLETED", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Conversion_ConverterFails_StatusIsFailed()
    {
        var created = await _client.CreateJobAsync("bmp");

        var processed = await factory.RunWorkerOnceAsync();
        Assert.True(processed);

        var response = await _client.SendWithTokenAsync(HttpMethod.Get, created.JobId, created.AccessToken);
        var status = await response.Content.ReadFromJsonAsync<GetConversionResponse>(TestContext.Current.CancellationToken);

        Assert.NotNull(status);
        Assert.Equal("Failed", status.Status);
        Assert.Equal(ErrorCodes.ProcessingFailed, status.ErrorCode);
        Assert.False(status.DownloadAvailable);
    }

    [Fact]
    public async Task Delete_CompletedJob_RemovesFilesAndAccess()
    {
        var created = await _client.CreateJobAsync();

        var processed = await factory.RunWorkerOnceAsync();
        Assert.True(processed);

        var response = await _client.SendWithTokenAsync(HttpMethod.Delete, created.JobId, created.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var responseAfterDelete = await _client.SendWithTokenAsync(HttpMethod.Get, created.JobId, created.AccessToken);
        await AssertAccessDeniedAsync(responseAfterDelete);

        Assert.Equal(0, factory.CountStoredFiles());
    }

    [Fact]
    public async Task Cleanup_AfterRetention_ExpiresJob()
    {
        var created = await _client.CreateJobAsync();

        var processed = await factory.RunWorkerOnceAsync();
        Assert.True(processed);

        factory.Clock.Advance(TimeSpan.FromMinutes(61));
        await factory.RunCleanupAsync();

        var job = await factory.QueryDbAsync(db => db.ConversionJobs.SingleAsync(j => j.Id == created.JobId));

        Assert.Equal(JobStatus.Expired, job.Status);
        Assert.Equal(0, factory.CountStoredFiles());

        var response = await _client.SendWithTokenAsync(HttpMethod.Get, created.JobId, created.AccessToken);

        await AssertAccessDeniedAsync(response);
    }

    [Fact]
    public async Task CreateConversion_TextNamedAsPng_Returns415()
    {
        var response = await _client.PostConversionAsync(TestFiles.Text, "photo.png", "jpg");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("UNSUPPORTED_FORMAT", problem.GetProperty("code").GetString());

        Assert.Equal(0, factory.CountStoredFiles());
    }

    private static async Task AssertAccessDeniedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("ACCESS_DENIED", problem.GetProperty("code").GetString());
    }
}