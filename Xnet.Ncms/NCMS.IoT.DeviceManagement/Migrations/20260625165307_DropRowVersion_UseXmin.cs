using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class DropRowVersion_UseXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "firmware",
                table: "upgrade_tasks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "firmware",
                table: "upgrade_task_devices");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "firmware",
                table: "upgrade_tasks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "firmware",
                table: "upgrade_task_devices",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "firmware",
                table: "upgrade_tasks");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "firmware",
                table: "upgrade_task_devices");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "firmware",
                table: "upgrade_tasks",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "firmware",
                table: "upgrade_task_devices",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
