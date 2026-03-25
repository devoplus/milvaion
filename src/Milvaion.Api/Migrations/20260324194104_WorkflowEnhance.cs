using Microsoft.EntityFrameworkCore.Migrations;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;

#nullable disable

namespace Milvaion.Api.Migrations;

/// <inheritdoc />
public partial class WorkflowEnhance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_JobOccurrences_WorkflowSteps_WorkflowStepId",
            table: "JobOccurrences");

        migrationBuilder.DropTable(
            name: "WorkflowSteps");

        migrationBuilder.DropIndex(
            name: "IX_JobOccurrences_WorkflowStepId",
            table: "JobOccurrences");

        migrationBuilder.AddColumn<WorkflowDefinition>(
            name: "Definition",
            table: "Workflows",
            type: "jsonb",
            nullable: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Definition",
            table: "Workflows");

        migrationBuilder.CreateTable(
            name: "WorkflowSteps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobId = table.Column<Guid>(type: "uuid", nullable: false),
                WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                Condition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                DataMappings = table.Column<string>(type: "jsonb", nullable: true),
                DelaySeconds = table.Column<int>(type: "integer", nullable: false),
                DependsOnStepIds = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                JobDataOverride = table.Column<string>(type: "jsonb", nullable: true),
                Order = table.Column<int>(type: "integer", nullable: false),
                PositionX = table.Column<double>(type: "double precision", nullable: true),
                PositionY = table.Column<double>(type: "double precision", nullable: true),
                StepName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowSteps", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkflowSteps_ScheduledJobs_JobId",
                    column: x => x.JobId,
                    principalTable: "ScheduledJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_WorkflowSteps_Workflows_WorkflowId",
                    column: x => x.WorkflowId,
                    principalTable: "Workflows",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_JobOccurrences_WorkflowStepId",
            table: "JobOccurrences",
            column: "WorkflowStepId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_JobId",
            table: "WorkflowSteps",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_WorkflowId",
            table: "WorkflowSteps",
            column: "WorkflowId");

        migrationBuilder.AddForeignKey(
            name: "FK_JobOccurrences_WorkflowSteps_WorkflowStepId",
            table: "JobOccurrences",
            column: "WorkflowStepId",
            principalTable: "WorkflowSteps",
            principalColumn: "Id");
    }
}
