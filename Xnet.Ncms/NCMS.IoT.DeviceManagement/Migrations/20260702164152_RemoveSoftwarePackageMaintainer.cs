using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSoftwarePackageMaintainer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Maintainer",
                schema: "software",
                table: "software_packages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Maintainer",
                schema: "software",
                table: "software_packages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
