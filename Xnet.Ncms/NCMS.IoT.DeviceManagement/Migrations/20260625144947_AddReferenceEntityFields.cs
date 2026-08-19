using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceEntityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MacAddress",
                table: "hardware_inventory",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(17)",
                oldMaxLength: 17);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivationWindowEnd",
                table: "hardware_inventory",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivationWindowStart",
                table: "hardware_inventory",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "IdentityClaims",
                table: "hardware_inventory",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "IdentityPolicy",
                table: "hardware_inventory",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "serial_only");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "hardware_inventory",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "PENDING_ACTIVATION");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "devices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "devices",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "devices",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "MacAddresses",
                table: "devices",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "devices",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "PROVISIONING");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "devices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "WanIpAddress",
                table: "devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "device_telemetry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CpuUsagePercent = table.Column<double>(type: "double precision", nullable: false),
                    RamUsageMb = table.Column<double>(type: "double precision", nullable: false),
                    RamTotalMb = table.Column<double>(type: "double precision", nullable: false),
                    StorageUsedMb = table.Column<double>(type: "double precision", nullable: false),
                    StorageTotalMb = table.Column<double>(type: "double precision", nullable: false),
                    UptimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    WanIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TemperatureCelsius = table.Column<double>(type: "double precision", nullable: true),
                    SignalStrengthRssi = table.Column<double>(type: "double precision", nullable: true),
                    SignalQualityRsrp = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_telemetry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_telemetry_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_telemetry_device_timestamp",
                table: "device_telemetry",
                columns: new[] { "DeviceId", "Timestamp" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_telemetry");

            migrationBuilder.DropColumn(
                name: "ActivationWindowEnd",
                table: "hardware_inventory");

            migrationBuilder.DropColumn(
                name: "ActivationWindowStart",
                table: "hardware_inventory");

            migrationBuilder.DropColumn(
                name: "IdentityClaims",
                table: "hardware_inventory");

            migrationBuilder.DropColumn(
                name: "IdentityPolicy",
                table: "hardware_inventory");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "hardware_inventory");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "MacAddresses",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "WanIpAddress",
                table: "devices");

            migrationBuilder.AlterColumn<string>(
                name: "MacAddress",
                table: "hardware_inventory",
                type: "character varying(17)",
                maxLength: 17,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
