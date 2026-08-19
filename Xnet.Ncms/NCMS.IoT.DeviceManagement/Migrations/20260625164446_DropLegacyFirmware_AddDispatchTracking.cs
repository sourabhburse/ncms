using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyFirmware_AddDispatchTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmwareAuditRecords",
                schema: "firmware");

            migrationBuilder.DropTable(
                name: "FirmwareDeploymentJobs",
                schema: "firmware");

            migrationBuilder.DropTable(
                name: "FirmwarePackageVariants",
                schema: "firmware");

            migrationBuilder.DropTable(
                name: "FirmwareCampaigns",
                schema: "firmware");

            migrationBuilder.DropTable(
                name: "FirmwareHardwareVariants",
                schema: "firmware");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "firmware",
                table: "upgrade_tasks",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcknowledgedAt",
                schema: "firmware",
                table: "upgrade_task_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "firmware",
                table: "upgrade_task_devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DispatchedAt",
                schema: "firmware",
                table: "upgrade_task_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                schema: "firmware",
                table: "upgrade_task_devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                schema: "firmware",
                table: "upgrade_task_devices",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                schema: "firmware",
                table: "upgrade_task_devices",
                type: "integer",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "firmware",
                table: "upgrade_tasks");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAt",
                schema: "firmware",
                table: "upgrade_task_devices");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "firmware",
                table: "upgrade_task_devices");

            migrationBuilder.DropColumn(
                name: "DispatchedAt",
                schema: "firmware",
                table: "upgrade_task_devices");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                schema: "firmware",
                table: "upgrade_task_devices");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                schema: "firmware",
                table: "upgrade_task_devices");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                schema: "firmware",
                table: "upgrade_task_devices");

            migrationBuilder.CreateTable(
                name: "FirmwareAuditRecords",
                schema: "firmware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmwareAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmwareCampaigns",
                schema: "firmware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmwarePackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RolloutConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    RolloutStrategy = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TargetSpecJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpgradeDurationHours = table.Column<decimal>(type: "numeric(6,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmwareCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmwareCampaigns_FirmwarePackages_FirmwarePackageId",
                        column: x => x.FirmwarePackageId,
                        principalSchema: "firmware",
                        principalTable: "FirmwarePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FirmwareHardwareVariants",
                schema: "firmware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChipsetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeviceTypeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxFirmwareSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PcbRevision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmwareHardwareVariants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmwareDeploymentJobs",
                schema: "firmware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    OriginalFirmwareVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmwareDeploymentJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmwareDeploymentJobs_FirmwareCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "firmware",
                        principalTable: "FirmwareCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FirmwarePackageVariants",
                schema: "firmware",
                columns: table => new
                {
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmwarePackageVariants", x => new { x.PackageId, x.VariantId });
                    table.ForeignKey(
                        name: "FK_FirmwarePackageVariants_FirmwareHardwareVariants_VariantId",
                        column: x => x.VariantId,
                        principalSchema: "firmware",
                        principalTable: "FirmwareHardwareVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FirmwarePackageVariants_FirmwarePackages_PackageId",
                        column: x => x.PackageId,
                        principalSchema: "firmware",
                        principalTable: "FirmwarePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmwareAuditRecords_EntityType_EntityId_Timestamp",
                schema: "firmware",
                table: "FirmwareAuditRecords",
                columns: new[] { "EntityType", "EntityId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmwareCampaigns_FirmwarePackageId",
                schema: "firmware",
                table: "FirmwareCampaigns",
                column: "FirmwarePackageId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmwareDeploymentJobs_CampaignId_Status",
                schema: "firmware",
                table: "FirmwareDeploymentJobs",
                columns: new[] { "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmwareDeploymentJobs_DeviceId_Status",
                schema: "firmware",
                table: "FirmwareDeploymentJobs",
                columns: new[] { "DeviceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmwarePackageVariants_VariantId",
                schema: "firmware",
                table: "FirmwarePackageVariants",
                column: "VariantId");
        }
    }
}
