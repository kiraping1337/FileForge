using FileForge.Domain.Entities;
using FileForge.Domain.Enums;
using FileForge.Domain.Exceptions;

namespace FileForge.UnitTests.Domain;

public class ConversionJobTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Retention = TimeSpan.FromHours(1);

    private static ConversionJob CreateJob() =>
        ConversionJob.Create("png", "jpg", "photo.png", 1024, Now, Retention);

    [Fact]
    public void Create_StartsAsPendingWithExpiry()
    {
        var job = CreateJob();

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(Now + Retention, job.ExpiresAtUtc);
        Assert.Equal(0, job.AttemptCount);
        Assert.NotEqual(Guid.Empty, job.Id);
    }

    [Fact]
    public void Create_NormalizesFormatsToLowerCase()
    {
        var job = ConversionJob.Create("PNG", "JPG", "photo.png", 1, Now, Retention);

        Assert.Equal("png", job.SourceFormat);
        Assert.Equal("jpg", job.TargetFormat);
    }

    [Fact]
    public void Create_RejectsSameSourceAndTarget()
    {
        Assert.Throws<DomainException>(() =>
            ConversionJob.Create("png", "PNG", "photo.png", 1, Now, Retention));
    }

    [Fact]
    public void Create_RejectsNonUtcTime()
    {
        var localTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<DomainException>(() =>
            ConversionJob.Create("png", "jpg", "photo.png", 1, localTime, Retention));
    }

    [Fact]
    public void Create_RejectsNegativeSize()
    {
        Assert.Throws<DomainException>(() =>
            ConversionJob.Create("png", "jpg", "photo.png", -1, Now, Retention));
    }

    [Fact]
    public void HappyPath_PendingProcessingCompleted()
    {
        var job = CreateJob();
        job.AddOriginalFile("original-key", "png", Now);

        job.StartProcessing(Now.AddSeconds(1));
        job.Complete("result-key", 512, Now.AddSeconds(3));

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(512L, job.OutputSizeBytes);
        Assert.Equal(2000L, job.ProcessingDurationMs);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(2, job.Files.Count);
        Assert.Contains(job.Files, f => f.Role == FileRole.Result && f.DetectedFormat == "jpg");
    }

    [Fact]
    public void Complete_FromPending_Throws()
    {
        var job = CreateJob();

        var ex = Assert.Throws<InvalidJobStateException>(() => job.Complete("result-key", 1, Now));

        Assert.Equal(JobStatus.Pending, ex.CurrentStatus);
        Assert.Equal(JobStatus.Pending, job.Status);
    }

    [Fact]
    public void StartProcessing_Twice_Throws()
    {
        var job = CreateJob();
        job.StartProcessing(Now);

        Assert.Throws<InvalidJobStateException>(() => job.StartProcessing(Now));
    }

    [Fact]
    public void ReturnToQueue_KeepsAttemptCount()
    {
        var job = CreateJob();
        job.StartProcessing(Now);

        job.ReturnToQueue();
        job.StartProcessing(Now.AddMinutes(1));

        Assert.Equal(JobStatus.Processing, job.Status);
        Assert.Equal(2, job.AttemptCount);
    }

    [Fact]
    public void Fail_BeforeStart_HasNoDuration()
    {
        var job = CreateJob();

        job.Fail("INVALID_FILE_CONTENT", Now);

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("INVALID_FILE_CONTENT", job.ErrorCode);
        Assert.Null(job.ProcessingDurationMs);
    }

    [Fact]
    public void Cancel_AfterCompleted_Throws()
    {
        var job = CreateJob();
        job.StartProcessing(Now);
        job.Complete("result-key", 1, Now);

        Assert.Throws<InvalidJobStateException>(() => job.Cancel(Now));
    }

    [Fact]
    public void Fail_AfterCompleted_Throws()
    {
        var job = CreateJob();
        job.StartProcessing(Now);
        job.Complete("result-key", 1, Now);

        Assert.Throws<InvalidJobStateException>(() => job.Fail("PROCESSING_FAILED", Now));
        Assert.Equal(JobStatus.Completed, job.Status);
    }

    [Fact]
    public void Expire_WhileProcessing_Throws()
    {
        var job = CreateJob();
        job.StartProcessing(Now);

        Assert.Throws<InvalidJobStateException>(() => job.Expire());
    }

    [Fact]
    public void AddOriginalFile_Twice_Throws()
    {
        var job = CreateJob();
        job.AddOriginalFile("key-1", "png", Now);

        Assert.Throws<DomainException>(() => job.AddOriginalFile("key-2", "png", Now));
    }

    [Fact]
    public void RequestCancellation_FromProcessing_RecordsTimeAndKeepsStatus()
    {
        var job = CreateJob();
        job.StartProcessing(Now);
        job.RequestCancellation(Now);
        Assert.Equal(Now, job.CancellationRequestedAtUtc);
        Assert.Equal(JobStatus.Processing, job.Status);
    }

    [Fact]
    public void RequestCancellation_FromPending_Throws()
    {
        var job = CreateJob();
        Assert.Throws<InvalidJobStateException>(() => job.RequestCancellation(Now));
    }

    [Fact]
    public void RequestCancellation_Twice_KeepsFirstTime()
    {
        var job = CreateJob();
        job.StartProcessing(Now);
        job.RequestCancellation(Now);
        job.RequestCancellation(Now.AddMinutes(1));
        Assert.Equal(Now, job.CancellationRequestedAtUtc);
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(61, true)]
    public void IsExpiredAt_ComparesWithExpiry(int minutesLater, bool expected)
    {
        var job = CreateJob();

        Assert.Equal(expected, job.IsExpiredAt(Now.AddMinutes(minutesLater)));
    }
}
