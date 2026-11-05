using FileForge.Domain.Entities;
using FileForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileForge.Infrastructure.Persistence.Configurations;

public class ConversionJobConfiguration : IEntityTypeConfiguration<ConversionJob>
{
    public void Configure(EntityTypeBuilder<ConversionJob> builder)
    {
        // Built from the enum so the constraint stays in sync with the code.
        var statuses = string.Join(", ", Enum.GetNames<JobStatus>().Select(s => $"'{s}'"));

        builder.ToTable("ConversionJobs", table =>
        {
            table.HasCheckConstraint("CK_ConversionJobs_Status", $"\"Status\" IN ({statuses})");
            table.HasCheckConstraint("CK_ConversionJobs_FileSizeBytes", "\"FileSizeBytes\" >= 0");
            table.HasCheckConstraint(
                "CK_ConversionJobs_OutputSizeBytes",
                "\"OutputSizeBytes\" IS NULL OR \"OutputSizeBytes\" >= 0");
            table.HasCheckConstraint("CK_ConversionJobs_AttemptCount", "\"AttemptCount\" >= 0");
            table.HasCheckConstraint("CK_ConversionJobs_Formats", "\"SourceFormat\" <> \"TargetFormat\"");
            table.HasCheckConstraint("CK_ConversionJobs_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
        });

        builder.HasKey(j => j.Id);

        // Ids are generated in code; without this EF would treat a new entity with a set id as existing.
        builder.Property(j => j.Id).ValueGeneratedNever();

        // Text instead of int: readable in SQL and safe if enum members are reordered.
        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(j => j.SourceFormat).HasMaxLength(ConversionJob.MaxFormatLength).IsRequired();
        builder.Property(j => j.TargetFormat).HasMaxLength(ConversionJob.MaxFormatLength).IsRequired();
        builder.Property(j => j.OriginalFileName).HasMaxLength(ConversionJob.MaxFileNameLength).IsRequired();
        builder.Property(j => j.ErrorCode).HasMaxLength(ConversionJob.MaxErrorCodeLength);

        // Restrict: file rows are removed by cleanup only after the physical file is deleted.
        builder.HasMany(j => j.Files)
            .WithOne()
            .HasForeignKey(f => f.JobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => new { j.Status, j.CreatedAtUtc }); // queue: oldest Pending first
        builder.HasIndex(j => j.ExpiresAtUtc);                   // cleanup: find expired jobs
    }
}
