using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class RenameSoftwareModuleToApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_software_inventory",
                schema: "public");

            migrationBuilder.DropTable(
                name: "software_installation_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "software_package_bundle_items",
                schema: "software");

            migrationBuilder.DropTable(
                name: "software_package_dependencies",
                schema: "software");

            migrationBuilder.DropTable(
                name: "software_package_product",
                schema: "software");

            migrationBuilder.DropTable(
                name: "software_task_device_items",
                schema: "software");

            migrationBuilder.DropTable(
                name: "software_task_devices",
                schema: "software");

            migrationBuilder.DropTable(
                name: "software_tasks",
                schema: "software");

            migrationBuilder.DropTable(
                name: "software_package_bundles",
                schema: "software");

            migrationBuilder.DropTable(
                name: "software_package_versions",
                schema: "software");

            migrationBuilder.DropTable(
                name: "software_packages",
                schema: "software");

            migrationBuilder.EnsureSchema(
                name: "application");

            migrationBuilder.CreateTable(
                name: "application_packages",
                schema: "application",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "application_package_versions",
                schema: "application",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PackageFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Md5Checksum = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    ReleaseNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_package_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_package_versions_application_packages_Applicati~",
                        column: x => x.ApplicationPackageId,
                        principalSchema: "application",
                        principalTable: "application_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "application_package_dependencies",
                schema: "application",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnApplicationPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionConstraint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_package_dependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_package_dependencies_application_package_versio~",
                        column: x => x.ApplicationPackageVersionId,
                        principalSchema: "application",
                        principalTable: "application_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_application_package_dependencies_application_packages_Depen~",
                        column: x => x.DependsOnApplicationPackageId,
                        principalSchema: "application",
                        principalTable: "application_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "application_package_product",
                schema: "application",
                columns: table => new
                {
                    ApplicationPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_package_product", x => new { x.ApplicationPackageVersionId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_application_package_product_application_package_versions_Ap~",
                        column: x => x.ApplicationPackageVersionId,
                        principalSchema: "application",
                        principalTable: "application_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_application_package_product_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "products",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "application_tasks",
                schema: "application",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    TargetApplicationPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Timeout = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_tasks_application_package_versions_TargetApplic~",
                        column: x => x.TargetApplicationPackageVersionId,
                        principalSchema: "application",
                        principalTable: "application_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_application_inventory",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstalledVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InstalledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_application_inventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_application_inventory_application_package_versions_I~",
                        column: x => x.InstalledVersionId,
                        principalSchema: "application",
                        principalTable: "application_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_application_inventory_application_packages_Applicati~",
                        column: x => x.ApplicationPackageId,
                        principalSchema: "application",
                        principalTable: "application_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_application_inventory_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_task_devices",
                schema: "application",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_task_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_task_devices_application_package_versions_Appli~",
                        column: x => x.ApplicationPackageVersionId,
                        principalSchema: "application",
                        principalTable: "application_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_application_task_devices_application_tasks_ApplicationTaskId",
                        column: x => x.ApplicationTaskId,
                        principalSchema: "application",
                        principalTable: "application_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_application_task_devices_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "application_installation_history",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationPackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApplicationTaskDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_installation_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_installation_history_application_package_versio~",
                        column: x => x.ApplicationPackageVersionId,
                        principalSchema: "application",
                        principalTable: "application_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_application_installation_history_application_task_devices_A~",
                        column: x => x.ApplicationTaskDeviceId,
                        principalSchema: "application",
                        principalTable: "application_task_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_application_installation_history_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_installation_history_ApplicationPackageVersionId",
                schema: "public",
                table: "application_installation_history",
                column: "ApplicationPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_application_installation_history_ApplicationTaskDeviceId",
                schema: "public",
                table: "application_installation_history",
                column: "ApplicationTaskDeviceId");

            migrationBuilder.CreateIndex(
                name: "ix_application_installation_history_device_occurred",
                schema: "public",
                table: "application_installation_history",
                columns: new[] { "DeviceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_application_package_dependencies_DependsOnApplicationPackag~",
                schema: "application",
                table: "application_package_dependencies",
                column: "DependsOnApplicationPackageId");

            migrationBuilder.CreateIndex(
                name: "ix_application_package_dependencies_version_target",
                schema: "application",
                table: "application_package_dependencies",
                columns: new[] { "ApplicationPackageVersionId", "DependsOnApplicationPackageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_application_package_product_product_id",
                schema: "application",
                table: "application_package_product",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ix_application_package_versions_package_enabled",
                schema: "application",
                table: "application_package_versions",
                columns: new[] { "ApplicationPackageId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "ix_application_package_versions_package_version_format",
                schema: "application",
                table: "application_package_versions",
                columns: new[] { "ApplicationPackageId", "Version", "PackageFormat" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_application_packages_name",
                schema: "application",
                table: "application_packages",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_task_devices_ApplicationPackageVersionId",
                schema: "application",
                table: "application_task_devices",
                column: "ApplicationPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "ix_application_task_devices_device_status",
                schema: "application",
                table: "application_task_devices",
                columns: new[] { "DeviceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_application_task_devices_task_active",
                schema: "application",
                table: "application_task_devices",
                columns: new[] { "ApplicationTaskId", "Status" },
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "ix_application_task_devices_task_device",
                schema: "application",
                table: "application_task_devices",
                columns: new[] { "ApplicationTaskId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_application_task_devices_active_device",
                schema: "application",
                table: "application_task_devices",
                column: "DeviceId",
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "ix_application_tasks_pending",
                schema: "application",
                table: "application_tasks",
                column: "Status",
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "ix_application_tasks_target_version_id",
                schema: "application",
                table: "application_tasks",
                column: "TargetApplicationPackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_device_application_inventory_ApplicationPackageId",
                schema: "public",
                table: "device_application_inventory",
                column: "ApplicationPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_device_application_inventory_InstalledVersionId",
                schema: "public",
                table: "device_application_inventory",
                column: "InstalledVersionId");

            migrationBuilder.CreateIndex(
                name: "ux_device_application_inventory_device_package",
                schema: "public",
                table: "device_application_inventory",
                columns: new[] { "DeviceId", "ApplicationPackageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_installation_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "application_package_dependencies",
                schema: "application");

            migrationBuilder.DropTable(
                name: "application_package_product",
                schema: "application");

            migrationBuilder.DropTable(
                name: "device_application_inventory",
                schema: "public");

            migrationBuilder.DropTable(
                name: "application_task_devices",
                schema: "application");

            migrationBuilder.DropTable(
                name: "application_tasks",
                schema: "application");

            migrationBuilder.DropTable(
                name: "application_package_versions",
                schema: "application");

            migrationBuilder.DropTable(
                name: "application_packages",
                schema: "application");

            migrationBuilder.EnsureSchema(
                name: "software");

            migrationBuilder.CreateTable(
                name: "software_package_bundles",
                schema: "software",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_package_bundles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "software_packages",
                schema: "software",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "software_package_versions",
                schema: "software",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwarePackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    PackageFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReleaseNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Sha256Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_package_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_software_package_versions_software_packages_SoftwarePackage~",
                        column: x => x.SoftwarePackageId,
                        principalSchema: "software",
                        principalTable: "software_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_software_inventory",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstalledVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwarePackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstalledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_software_inventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_software_inventory_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_device_software_inventory_software_package_versions_Install~",
                        column: x => x.InstalledVersionId,
                        principalSchema: "software",
                        principalTable: "software_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_software_inventory_software_packages_SoftwarePackage~",
                        column: x => x.SoftwarePackageId,
                        principalSchema: "software",
                        principalTable: "software_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "software_package_bundle_items",
                schema: "software",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwarePackageBundleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwarePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_package_bundle_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_software_package_bundle_items_software_package_bundles_Soft~",
                        column: x => x.SoftwarePackageBundleId,
                        principalSchema: "software",
                        principalTable: "software_package_bundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_software_package_bundle_items_software_package_versions_Sof~",
                        column: x => x.SoftwarePackageVersionId,
                        principalSchema: "software",
                        principalTable: "software_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "software_package_dependencies",
                schema: "software",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnSoftwarePackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwarePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionConstraint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_package_dependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_software_package_dependencies_software_package_versions_Sof~",
                        column: x => x.SoftwarePackageVersionId,
                        principalSchema: "software",
                        principalTable: "software_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_software_package_dependencies_software_packages_DependsOnSo~",
                        column: x => x.DependsOnSoftwarePackageId,
                        principalSchema: "software",
                        principalTable: "software_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "software_package_product",
                schema: "software",
                columns: table => new
                {
                    SoftwarePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_package_product", x => new { x.SoftwarePackageVersionId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_software_package_product_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "products",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_software_package_product_software_package_versions_Software~",
                        column: x => x.SoftwarePackageVersionId,
                        principalSchema: "software",
                        principalTable: "software_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "software_tasks",
                schema: "software",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetSoftwarePackageBundleId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetSoftwarePackageVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Timeout = table.Column<TimeSpan>(type: "interval", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_software_tasks_software_package_bundles_TargetSoftwarePacka~",
                        column: x => x.TargetSoftwarePackageBundleId,
                        principalSchema: "software",
                        principalTable: "software_package_bundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_software_tasks_software_package_versions_TargetSoftwarePack~",
                        column: x => x.TargetSoftwarePackageVersionId,
                        principalSchema: "software",
                        principalTable: "software_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "software_task_devices",
                schema: "software",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwareTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_task_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_software_task_devices_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_software_task_devices_software_tasks_SoftwareTaskId",
                        column: x => x.SoftwareTaskId,
                        principalSchema: "software",
                        principalTable: "software_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "software_installation_history",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwarePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwareTaskDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_installation_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_software_installation_history_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_software_installation_history_software_package_versions_Sof~",
                        column: x => x.SoftwarePackageVersionId,
                        principalSchema: "software",
                        principalTable: "software_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_software_installation_history_software_task_devices_Softwar~",
                        column: x => x.SoftwareTaskDeviceId,
                        principalSchema: "software",
                        principalTable: "software_task_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "software_task_device_items",
                schema: "software",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwarePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoftwareTaskDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InstallOrder = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_task_device_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_software_task_device_items_software_package_versions_Softwa~",
                        column: x => x.SoftwarePackageVersionId,
                        principalSchema: "software",
                        principalTable: "software_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_software_task_device_items_software_task_devices_SoftwareTa~",
                        column: x => x.SoftwareTaskDeviceId,
                        principalSchema: "software",
                        principalTable: "software_task_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_software_inventory_InstalledVersionId",
                schema: "public",
                table: "device_software_inventory",
                column: "InstalledVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_device_software_inventory_SoftwarePackageId",
                schema: "public",
                table: "device_software_inventory",
                column: "SoftwarePackageId");

            migrationBuilder.CreateIndex(
                name: "ux_device_software_inventory_device_package",
                schema: "public",
                table: "device_software_inventory",
                columns: new[] { "DeviceId", "SoftwarePackageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_software_installation_history_device_occurred",
                schema: "public",
                table: "software_installation_history",
                columns: new[] { "DeviceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_software_installation_history_SoftwarePackageVersionId",
                schema: "public",
                table: "software_installation_history",
                column: "SoftwarePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_software_installation_history_SoftwareTaskDeviceId",
                schema: "public",
                table: "software_installation_history",
                column: "SoftwareTaskDeviceId");

            migrationBuilder.CreateIndex(
                name: "ix_software_package_bundle_items_bundle_order",
                schema: "software",
                table: "software_package_bundle_items",
                columns: new[] { "SoftwarePackageBundleId", "InstallOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_software_package_bundle_items_SoftwarePackageVersionId",
                schema: "software",
                table: "software_package_bundle_items",
                column: "SoftwarePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "ix_software_package_bundles_name",
                schema: "software",
                table: "software_package_bundles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_software_package_dependencies_DependsOnSoftwarePackageId",
                schema: "software",
                table: "software_package_dependencies",
                column: "DependsOnSoftwarePackageId");

            migrationBuilder.CreateIndex(
                name: "ix_software_package_dependencies_version_target",
                schema: "software",
                table: "software_package_dependencies",
                columns: new[] { "SoftwarePackageVersionId", "DependsOnSoftwarePackageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_software_package_product_product_id",
                schema: "software",
                table: "software_package_product",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ix_software_package_versions_package_enabled",
                schema: "software",
                table: "software_package_versions",
                columns: new[] { "SoftwarePackageId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "ix_software_package_versions_package_version_format",
                schema: "software",
                table: "software_package_versions",
                columns: new[] { "SoftwarePackageId", "Version", "PackageFormat" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_software_packages_name",
                schema: "software",
                table: "software_packages",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_software_task_device_items_device_order",
                schema: "software",
                table: "software_task_device_items",
                columns: new[] { "SoftwareTaskDeviceId", "InstallOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_software_task_device_items_version_status",
                schema: "software",
                table: "software_task_device_items",
                columns: new[] { "SoftwarePackageVersionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_software_task_devices_device_status",
                schema: "software",
                table: "software_task_devices",
                columns: new[] { "DeviceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_software_task_devices_task_active",
                schema: "software",
                table: "software_task_devices",
                columns: new[] { "SoftwareTaskId", "Status" },
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "ix_software_task_devices_task_device",
                schema: "software",
                table: "software_task_devices",
                columns: new[] { "SoftwareTaskId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_software_task_devices_active_device",
                schema: "software",
                table: "software_task_devices",
                column: "DeviceId",
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "ix_software_tasks_pending",
                schema: "software",
                table: "software_tasks",
                column: "Status",
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "ix_software_tasks_target_version_id",
                schema: "software",
                table: "software_tasks",
                column: "TargetSoftwarePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_software_tasks_TargetSoftwarePackageBundleId",
                schema: "software",
                table: "software_tasks",
                column: "TargetSoftwarePackageBundleId");
        }
    }
}
