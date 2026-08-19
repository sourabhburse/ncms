using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSoftwarePackageFirmwareCompatibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "software_package_firmware_compat",
                schema: "software");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "software_package_firmware_compat",
                schema: "software",
                columns: table => new
                {
                    SoftwarePackageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmwareId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_package_firmware_compat", x => new { x.SoftwarePackageVersionId, x.FirmwareId });
                    table.ForeignKey(
                        name: "FK_software_package_firmware_compat_FirmwarePackages_FirmwareId",
                        column: x => x.FirmwareId,
                        principalSchema: "firmware",
                        principalTable: "FirmwarePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_software_package_firmware_compat_software_package_versions_~",
                        column: x => x.SoftwarePackageVersionId,
                        principalSchema: "software",
                        principalTable: "software_package_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_software_package_firmware_compat_firmware_id",
                schema: "software",
                table: "software_package_firmware_compat",
                column: "FirmwareId");
        }
    }
}
