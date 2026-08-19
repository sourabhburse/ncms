using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class DropFirmwareStatusLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishedAt",
                schema: "firmware",
                table: "FirmwarePackages");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "firmware",
                table: "FirmwarePackages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                schema: "firmware",
                table: "FirmwarePackages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "firmware",
                table: "FirmwarePackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
