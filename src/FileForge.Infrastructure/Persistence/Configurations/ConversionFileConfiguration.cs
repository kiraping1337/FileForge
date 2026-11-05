using FileForge.Domain.Entities;
using FileForge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileForge.Infrastructure.Persistence.Configurations;

public class ConversionFileConfiguration : IEntityTypeConfiguration<ConversionFile>
{
    public void Configure(EntityTypeBuilder<ConversionFile> builder)
    {
        var roles = string.Join(", ", Enum.GetNames<FileRole>().Select(r => $"'{r}'"));

        builder.ToTable("ConversionFiles", table =>
        {
            table.HasCheckConstraint("CK_ConversionFiles_Role", $"\"Role\" IN ({roles})");
            table.HasCheckConstraint("CK_ConversionFiles_FileSizeBytes", "\"FileSizeBytes\" >= 0");
        });

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Role)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(f => f.StorageKey).HasMaxLength(ConversionFile.MaxStorageKeyLength).IsRequired();
        builder.Property(f => f.DetectedFormat).HasMaxLength(ConversionJob.MaxFormatLength).IsRequired();

        builder.HasIndex(f => f.StorageKey).IsUnique();

        // Also covers lookups by JobId alone (leftmost prefix), so no separate JobId index is needed.
        builder.HasIndex(f => new { f.JobId, f.Role }).IsUnique();
    }
}
