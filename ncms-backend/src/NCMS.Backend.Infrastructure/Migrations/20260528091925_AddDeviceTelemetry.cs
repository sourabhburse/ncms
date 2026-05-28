using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceTelemetries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CpuUsagePercent = table.Column<double>(type: "double precision", nullable: false),
                    RamUsageMb = table.Column<double>(type: "double precision", nullable: false),
                    RamTotalMb = table.Column<double>(type: "double precision", nullable: false),
                    StorageUsedMb = table.Column<double>(type: "double precision", nullable: false),
                    StorageTotalMb = table.Column<double>(type: "double precision", nullable: false),
                    UptimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    WanIp = table.Column<string>(type: "text", nullable: true),
                    TemperatureCelsius = table.Column<double>(type: "double precision", nullable: true),
                    SignalStrengthRssi = table.Column<double>(type: "double precision", nullable: true),
                    SignalQualityRsrp = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTelemetries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceTelemetries_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTelemetries_DeviceId_Timestamp",
                table: "DeviceTelemetries",
                columns: new[] { "DeviceId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceTelemetries");
        }
    }
}
