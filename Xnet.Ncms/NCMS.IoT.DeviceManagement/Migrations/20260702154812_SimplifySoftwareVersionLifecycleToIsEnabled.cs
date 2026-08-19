using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class SimplifySoftwareVersionLifecycleToIsEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_software_package_versions_package_status",
                schema: "software",
                table: "software_package_versions");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "software",
                table: "software_package_versions");

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "software",
                table: "software_package_versions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "ix_software_package_versions_package_enabled",
                schema: "software",
                table: "software_package_versions",
                columns: new[] { "SoftwarePackageId", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_software_package_versions_package_enabled",
                schema: "software",
                table: "software_package_versions");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "software",
                table: "software_package_versions");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "software",
                table: "software_package_versions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_software_package_versions_package_status",
                schema: "software",
                table: "software_package_versions",
                columns: new[] { "SoftwarePackageId", "Status" });
        }
    }
}
