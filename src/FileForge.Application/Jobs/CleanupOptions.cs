namespace FileForge.Application.Jobs;

public sealed class CleanupOptions
{
    public const string SectionName = "Cleanup";

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Max jobs (and tokens) handled per run, so one run never loads the whole table.</summary>
    public int BatchSize { get; set; } = 200;

    /// <summary>
    /// A file with no row is deleted only if it is at least this old. A fresh upload is written to disk
    /// a moment before its row is saved, and such a file must not be mistaken for an orphan.
    /// </summary>
    public TimeSpan OrphanFileMinAge { get; set; } = TimeSpan.FromHours(1);
}
