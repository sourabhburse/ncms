using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class SoftwarePackageTagsAndDropTargetPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                schema: "software",
                table: "software_packages");

            migrationBuilder.DropColumn(
                name: "TargetPlatform",
                schema: "software",
                table: "software_package_versions");

            migrationBuilder.AddColumn<List<string>>(
                name: "Tags",
                schema: "software",
                table: "software_packages",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tags",
                schema: "software",
                table: "software_packages");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "software",
                table: "software_packages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetPlatform",
                schema: "software",
                table: "software_package_versions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
