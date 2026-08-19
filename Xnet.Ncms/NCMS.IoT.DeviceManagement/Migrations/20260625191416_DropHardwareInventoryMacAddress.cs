using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class DropHardwareInventoryMacAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE hardware_inventory
                SET "IdentityClaims" = CASE
                    WHEN "IdentityClaims" ? 'base_mac' THEN "IdentityClaims"
                    ELSE jsonb_set(
                        "IdentityClaims",
                        '{base_mac}',
                        to_jsonb(regexp_replace("MacAddress", '[:-]', '', 'g')),
                        true)
                END
                WHERE "MacAddress" IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "ix_hardware_inventory_mac_address",
                table: "hardware_inventory");

            migrationBuilder.DropColumn(
                name: "MacAddress",
                table: "hardware_inventory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MacAddress",
                table: "hardware_inventory",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE hardware_inventory
                SET "MacAddress" = COALESCE(
                    "IdentityClaims" ->> 'base_mac',
                    '')
                WHERE "MacAddress" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_hardware_inventory_mac_address",
                table: "hardware_inventory",
                column: "MacAddress",
                unique: true);
        }
    }
}
