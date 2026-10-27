using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversionJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceFormat = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetFormat = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    OutputSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProcessingDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversionJobs", x => x.Id);
                    table.CheckConstraint("CK_ConversionJobs_AttemptCount", "\"AttemptCount\" >= 0");
                    table.CheckConstraint("CK_ConversionJobs_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_ConversionJobs_FileSizeBytes", "\"FileSizeBytes\" >= 0");
                    table.CheckConstraint("CK_ConversionJobs_Formats", "\"SourceFormat\" <> \"TargetFormat\"");
                    table.CheckConstraint("CK_ConversionJobs_OutputSizeBytes", "\"OutputSizeBytes\" IS NULL OR \"OutputSizeBytes\" >= 0");
                    table.CheckConstraint("CK_ConversionJobs_Status", "\"Status\" IN ('Pending', 'Processing', 'Completed', 'Failed', 'Expired', 'Cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "ConversionFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DetectedFormat = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversionFiles", x => x.Id);
                    table.CheckConstraint("CK_ConversionFiles_FileSizeBytes", "\"FileSizeBytes\" >= 0");
                    table.CheckConstraint("CK_ConversionFiles_Role", "\"Role\" IN ('Original', 'Result')");
                    table.ForeignKey(
                        name: "FK_ConversionFiles_ConversionJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "ConversionJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobAccessTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobAccessTokens", x => x.Id);
                    table.CheckConstraint("CK_JobAccessTokens_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_JobAccessTokens_TokenHashLength", "octet_length(\"TokenHash\") = 32");
                    table.ForeignKey(
                        name: "FK_JobAccessTokens_ConversionJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "ConversionJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversionFiles_JobId_Role",
                table: "ConversionFiles",
                columns: new[] { "JobId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversionFiles_StorageKey",
                table: "ConversionFiles",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversionJobs_ExpiresAtUtc",
                table: "ConversionJobs",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ConversionJobs_Status_CreatedAtUtc",
                table: "ConversionJobs",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_JobAccessTokens_ExpiresAtUtc",
                table: "JobAccessTokens",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_JobAccessTokens_JobId",
                table: "JobAccessTokens",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobAccessTokens_TokenHash",
                table: "JobAccessTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversionFiles");

            migrationBuilder.DropTable(
                name: "JobAccessTokens");

            migrationBuilder.DropTable(
                name: "ConversionJobs");
        }
    }
}
