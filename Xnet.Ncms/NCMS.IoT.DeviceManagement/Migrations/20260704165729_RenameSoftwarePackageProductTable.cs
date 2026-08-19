using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCMS.IoT.DeviceManagement.Migrations
{
    /// <inheritdoc />
    public partial class RenameSoftwarePackageProductTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_software_package_product_compat_products_ProductId",
                schema: "software",
                table: "software_package_product_compat");

            migrationBuilder.DropForeignKey(
                name: "FK_software_package_product_compat_software_package_versions_S~",
                schema: "software",
                table: "software_package_product_compat");

            migrationBuilder.DropPrimaryKey(
                name: "PK_software_package_product_compat",
                schema: "software",
                table: "software_package_product_compat");

            migrationBuilder.RenameTable(
                name: "software_package_product_compat",
                schema: "software",
                newName: "software_package_product",
                newSchema: "software");

            migrationBuilder.RenameIndex(
                name: "ix_software_package_product_compat_product_id",
                schema: "software",
                table: "software_package_product",
                newName: "ix_software_package_product_product_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_software_package_product",
                schema: "software",
                table: "software_package_product",
                columns: new[] { "SoftwarePackageVersionId", "ProductId" });

            migrationBuilder.AddForeignKey(
                name: "FK_software_package_product_products_ProductId",
                schema: "software",
                table: "software_package_product",
                column: "ProductId",
                principalSchema: "products",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_software_package_product_software_package_versions_Software~",
                schema: "software",
                table: "software_package_product",
                column: "SoftwarePackageVersionId",
                principalSchema: "software",
                principalTable: "software_package_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_software_package_product_products_ProductId",
                schema: "software",
                table: "software_package_product");

            migrationBuilder.DropForeignKey(
                name: "FK_software_package_product_software_package_versions_Software~",
                schema: "software",
                table: "software_package_product");

            migrationBuilder.DropPrimaryKey(
                name: "PK_software_package_product",
                schema: "software",
                table: "software_package_product");

            migrationBuilder.RenameTable(
                name: "software_package_product",
                schema: "software",
                newName: "software_package_product_compat",
                newSchema: "software");

            migrationBuilder.RenameIndex(
                name: "ix_software_package_product_product_id",
                schema: "software",
                table: "software_package_product_compat",
                newName: "ix_software_package_product_compat_product_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_software_package_product_compat",
                schema: "software",
                table: "software_package_product_compat",
                columns: new[] { "SoftwarePackageVersionId", "ProductId" });

            migrationBuilder.AddForeignKey(
                name: "FK_software_package_product_compat_products_ProductId",
                schema: "software",
                table: "software_package_product_compat",
                column: "ProductId",
                principalSchema: "products",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_software_package_product_compat_software_package_versions_S~",
                schema: "software",
                table: "software_package_product_compat",
                column: "SoftwarePackageVersionId",
                principalSchema: "software",
                principalTable: "software_package_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
