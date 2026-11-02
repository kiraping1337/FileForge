using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationRequested : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationRequestedAtUtc",
                table: "ConversionJobs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationRequestedAtUtc",
                table: "ConversionJobs");
        }
    }
}
