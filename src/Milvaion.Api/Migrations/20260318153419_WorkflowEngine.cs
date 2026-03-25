using Microsoft.EntityFrameworkCore.Migrations;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;

#nullable disable

namespace Milvaion.Api.Migrations;

/// <inheritdoc />
public partial class WorkflowEngine : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "StepRetryCount",
            table: "JobOccurrences",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "StepScheduledAt",
            table: "JobOccurrences",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "StepStatus",
            table: "JobOccurrences",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "WorkflowRunId",
            table: "JobOccurrences",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "WorkflowStepId",
            table: "JobOccurrences",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Workflows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                FailureStrategy = table.Column<int>(type: "integer", nullable: false),
                MaxStepRetries = table.Column<int>(type: "integer", nullable: false),
                TimeoutSeconds = table.Column<int>(type: "integer", nullable: true),
                Version = table.Column<int>(type: "integer", nullable: false),
                CronExpression = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                LastScheduledRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Versions = table.Column<List<WorkflowSnapshot>>(type: "jsonb", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Workflows", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                WorkflowVersion = table.Column<int>(type: "integer", nullable: false),
                CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DurationMs = table.Column<long>(type: "bigint", nullable: true),
                TriggerReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Error = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowRuns", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkflowRuns_Workflows_WorkflowId",
                    column: x => x.WorkflowId,
                    principalTable: "Workflows",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowSteps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                JobId = table.Column<Guid>(type: "uuid", nullable: false),
                StepName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Order = table.Column<int>(type: "integer", nullable: false),
                DependsOnStepIds = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Condition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                DataMappings = table.Column<string>(type: "jsonb", nullable: true),
                DelaySeconds = table.Column<int>(type: "integer", nullable: false),
                JobDataOverride = table.Column<string>(type: "jsonb", nullable: true),
                PositionX = table.Column<double>(type: "double precision", nullable: true),
                PositionY = table.Column<double>(type: "double precision", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
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
            name: "IX_JobOccurrences_WorkflowRunId",
            table: "JobOccurrences",
            column: "WorkflowRunId");

        migrationBuilder.CreateIndex(
            name: "IX_JobOccurrences_WorkflowStepId",
            table: "JobOccurrences",
            column: "WorkflowStepId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowRuns_WorkflowId",
            table: "WorkflowRuns",
            column: "WorkflowId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_JobId",
            table: "WorkflowSteps",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowSteps_WorkflowId",
            table: "WorkflowSteps",
            column: "WorkflowId");

        migrationBuilder.AddForeignKey(
            name: "FK_JobOccurrences_WorkflowRuns_WorkflowRunId",
            table: "JobOccurrences",
            column: "WorkflowRunId",
            principalTable: "WorkflowRuns",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_JobOccurrences_WorkflowSteps_WorkflowStepId",
            table: "JobOccurrences",
            column: "WorkflowStepId",
            principalTable: "WorkflowSteps",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_JobOccurrences_WorkflowRuns_WorkflowRunId",
            table: "JobOccurrences");

        migrationBuilder.DropForeignKey(
            name: "FK_JobOccurrences_WorkflowSteps_WorkflowStepId",
            table: "JobOccurrences");

        migrationBuilder.DropTable(
            name: "WorkflowRuns");

        migrationBuilder.DropTable(
            name: "WorkflowSteps");

        migrationBuilder.DropTable(
            name: "Workflows");

        migrationBuilder.DropIndex(
            name: "IX_JobOccurrences_WorkflowRunId",
            table: "JobOccurrences");

        migrationBuilder.DropIndex(
            name: "IX_JobOccurrences_WorkflowStepId",
            table: "JobOccurrences");

        migrationBuilder.DropColumn(
            name: "StepRetryCount",
            table: "JobOccurrences");

        migrationBuilder.DropColumn(
            name: "StepScheduledAt",
            table: "JobOccurrences");

        migrationBuilder.DropColumn(
            name: "StepStatus",
            table: "JobOccurrences");

        migrationBuilder.DropColumn(
            name: "WorkflowRunId",
            table: "JobOccurrences");

        migrationBuilder.DropColumn(
            name: "WorkflowStepId",
            table: "JobOccurrences");
    }
}
