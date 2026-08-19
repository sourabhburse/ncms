using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "firmware");

            migrationBuilder.EnsureSchema(
                name: "products");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "FirmwareAuditRecords",
                schema: "firmware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmwareAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmwareHardwareVariants",
                schema: "firmware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceTypeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PcbRevision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChipsetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxFirmwareSizeBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmwareHardwareVariants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmwarePackages",
                schema: "firmware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ReleaseNotes = table.Column<string>(type: "text", nullable: false),
                    DeviceTypeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BinaryChecksum = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Md5Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DigitalSignature = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MinRequiredFirmwareVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmwarePackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                schema: "products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmwareCampaigns",
                schema: "firmware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmwarePackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TargetSpecJson = table.Column<string>(type: "jsonb", nullable: false),
                    RolloutStrategy = table.Column<int>(type: "integer", nullable: false),
                    RolloutConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpgradeDurationHours = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "upgrade_tasks",
                schema: "firmware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmwareId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpgradeTimeout = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upgrade_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_upgrade_tasks_FirmwarePackages_FirmwareId",
                        column: x => x.FirmwareId,
                        principalSchema: "firmware",
                        principalTable: "FirmwarePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_types",
                schema: "products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_types_product_categories_ProductCategoryId",
                        column: x => x.ProductCategoryId,
                        principalSchema: "products",
                        principalTable: "product_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FirmwareDeploymentJobs",
                schema: "firmware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFirmwareVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                name: "products",
                schema: "products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SupportsFirmwareUpgrade = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SupportsConfiguration = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SupportsRemoteConfiguration = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SupportsCommands = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SupportsRealtimeLogs = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SupportsTelemetry = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_products_product_types_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalSchema: "products",
                        principalTable: "product_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "firmware_products",
                schema: "firmware",
                columns: table => new
                {
                    FirmwareId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_firmware_products", x => new { x.FirmwareId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_firmware_products_FirmwarePackages_FirmwareId",
                        column: x => x.FirmwareId,
                        principalSchema: "firmware",
                        principalTable: "FirmwarePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_firmware_products_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "products",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hardware_inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MacAddress = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    IsProvisioned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hardware_inventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hardware_inventory_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "products",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HardwareInventoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FirmwareVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AgentVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    HardwareModel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_devices_hardware_inventory_HardwareInventoryId",
                        column: x => x.HardwareInventoryId,
                        principalTable: "hardware_inventory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_certificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Thumbprint = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CertificatePem = table.Column<string>(type: "text", nullable: false),
                    SubjectName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_certificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_certificates_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_events_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_records",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Topic = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    QosLevel = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_telemetry_records_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "upgrade_task_devices",
                schema: "firmware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UpgradeTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PreviousFirmwareVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetFirmwareVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upgrade_task_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_upgrade_task_devices_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_upgrade_task_devices_upgrade_tasks_UpgradeTaskId",
                        column: x => x.UpgradeTaskId,
                        principalSchema: "firmware",
                        principalTable: "upgrade_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_certificates_device_id_is_active",
                table: "device_certificates",
                columns: new[] { "DeviceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_device_certificates_thumbprint",
                table: "device_certificates",
                column: "Thumbprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_events_device_id_event_type",
                table: "device_events",
                columns: new[] { "DeviceId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "ix_device_events_device_id_timestamp",
                table: "device_events",
                columns: new[] { "DeviceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_devices_HardwareInventoryId",
                table: "devices",
                column: "HardwareInventoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_firmware_products_product_id",
                schema: "firmware",
                table: "firmware_products",
                column: "ProductId");

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
                name: "ix_firmware_packages_devicetypecode_version",
                schema: "firmware",
                table: "FirmwarePackages",
                columns: new[] { "DeviceTypeCode", "Version" });

            migrationBuilder.CreateIndex(
                name: "ix_firmware_packages_name_version_type",
                schema: "firmware",
                table: "FirmwarePackages",
                columns: new[] { "Name", "Version", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmwarePackageVariants_VariantId",
                schema: "firmware",
                table: "FirmwarePackageVariants",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_inventory_mac_address",
                table: "hardware_inventory",
                column: "MacAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hardware_inventory_product_id",
                table: "hardware_inventory",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_inventory_serial_number",
                table: "hardware_inventory",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_name",
                schema: "products",
                table: "product_categories",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_product_types_category_name",
                schema: "products",
                table: "product_types",
                columns: new[] { "ProductCategoryId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_products_supports_firmware_upgrade",
                schema: "products",
                table: "products",
                column: "SupportsFirmwareUpgrade",
                filter: "\"SupportsFirmwareUpgrade\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "ix_products_type_name",
                schema: "products",
                table: "products",
                columns: new[] { "ProductTypeId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_telemetry_records_device_id_timestamp",
                table: "telemetry_records",
                columns: new[] { "DeviceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_upgrade_task_devices_device_status",
                schema: "firmware",
                table: "upgrade_task_devices",
                columns: new[] { "DeviceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_upgrade_task_devices_task_active",
                schema: "firmware",
                table: "upgrade_task_devices",
                columns: new[] { "UpgradeTaskId", "Status" },
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "ix_upgrade_task_devices_task_device",
                schema: "firmware",
                table: "upgrade_task_devices",
                columns: new[] { "UpgradeTaskId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_upgrade_tasks_firmware_id",
                schema: "firmware",
                table: "upgrade_tasks",
                column: "FirmwareId");

            migrationBuilder.CreateIndex(
                name: "ix_upgrade_tasks_pending",
                schema: "firmware",
                table: "upgrade_tasks",
                column: "Status",
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_certificates");

            migrationBuilder.DropTable(
                name: "device_events");

            migrationBuilder.DropTable(
                name: "firmware_products",
                schema: "firmware");

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
                name: "telemetry_records");

            migrationBuilder.DropTable(
                name: "upgrade_task_devices",
                schema: "firmware");

            migrationBuilder.DropTable(
                name: "FirmwareCampaigns",
                schema: "firmware");

            migrationBuilder.DropTable(
                name: "FirmwareHardwareVariants",
                schema: "firmware");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "upgrade_tasks",
                schema: "firmware");

            migrationBuilder.DropTable(
                name: "hardware_inventory");

            migrationBuilder.DropTable(
                name: "FirmwarePackages",
                schema: "firmware");

            migrationBuilder.DropTable(
                name: "products",
                schema: "products");

            migrationBuilder.DropTable(
                name: "product_types",
                schema: "products");

            migrationBuilder.DropTable(
                name: "product_categories",
                schema: "products");
        }
    }
}
