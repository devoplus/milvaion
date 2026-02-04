using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Milvaion.Api.Migrations;

/// <inheritdoc />
public partial class AddExternalJobSupport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ExternalJobId",
            table: "ScheduledJobs",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsExternal",
            table: "ScheduledJobs",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "ExternalJobId",
            table: "JobOccurrences",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExternalJobId",
            table: "ScheduledJobs");

        migrationBuilder.DropColumn(
            name: "IsExternal",
            table: "ScheduledJobs");

        migrationBuilder.DropColumn(
            name: "ExternalJobId",
            table: "JobOccurrences");
    }
}
