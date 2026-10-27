using FileForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileForge.Infrastructure.Persistence.Configurations;

public class JobAccessTokenConfiguration : IEntityTypeConfiguration<JobAccessToken>
{
    public void Configure(EntityTypeBuilder<JobAccessToken> builder)
    {
        builder.ToTable("JobAccessTokens", table =>
        {
            table.HasCheckConstraint(
                "CK_JobAccessTokens_TokenHashLength",
                $"octet_length(\"TokenHash\") = {JobAccessToken.HashLengthBytes}");
            table.HasCheckConstraint("CK_JobAccessTokens_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TokenHash).IsRequired();

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.ExpiresAtUtc); // cleanup of expired tokens

        // Cascade is safe: a token has no physical file behind it.
        // EF adds the JobId index for this FK automatically.
        builder.HasOne<ConversionJob>()
            .WithMany()
            .HasForeignKey(t => t.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
