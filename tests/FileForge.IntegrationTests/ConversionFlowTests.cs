using System.Net;
using System.Net.Http.Json;
using FileForge.Api.Contracts;
using FileForge.Application.Jobs;
using FileForge.Domain.Enums;
using FileForge.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FileForge.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ConversionFlowTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateConversion_ValidPng_Returns202AndStoresPendingJob()
    {
        var response = await _client.PostConversionAsync(TestFiles.Png, "photo.png", "jpg");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateConversionResponse>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal("Pending", created.Status);

        // What is really in the database: the job, its original file and only the HASH of the token.
        var job = await factory.QueryDbAsync(db => db.ConversionJobs
            .Include(j => j.Files)
            .SingleAsync(j => j.Id == created.JobId));
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal("png", job.SourceFormat);
        Assert.Single(job.Files, f => f.Role == FileRole.Original);

        var storedHash = await factory.QueryDbAsync(db => db.JobAccessTokens
            .Where(t => t.JobId == created.JobId)
            .Select(t => t.TokenHash)
            .SingleAsync());
        Assert.Equal(AccessTokens.TryHash(created.AccessToken), storedHash);

        Assert.Equal(1, factory.CountStoredFiles());
    }

    [Fact]
    public async Task FullFlow_PendingProcessingCompleted_StatusShowsDownload()
    {
        var created = await _client.CreateJobAsync();

        var processed = await factory.RunWorkerOnceAsync();

        Assert.True(processed);
        var response = await _client.SendWithTokenAsync(HttpMethod.Get, created.JobId, created.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<GetConversionResponse>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(status);
        Assert.Equal("Completed", status.Status);
        Assert.True(status.DownloadAvailable);
        Assert.Equal((long?)TestFiles.Png.Length, status.OutputSizeBytes);
    }
}
