using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveDeviceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_upgrade_task_devices_active_device",
                schema: "firmware",
                table: "upgrade_task_devices",
                column: "DeviceId",
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "ux_configure_task_devices_active_device",
                schema: "config",
                table: "configure_task_devices",
                column: "DeviceId",
                unique: true,
                filter: "\"Status\" IN (0, 10)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_upgrade_task_devices_active_device",
                schema: "firmware",
                table: "upgrade_task_devices");

            migrationBuilder.DropIndex(
                name: "ux_configure_task_devices_active_device",
                schema: "config",
                table: "configure_task_devices");
        }
    }
}
