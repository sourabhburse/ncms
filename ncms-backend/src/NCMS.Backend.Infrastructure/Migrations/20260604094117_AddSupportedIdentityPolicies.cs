using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using NCMS.Backend.Core.Entities;

#nullable disable

namespace NCMS.Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportedIdentityPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<ProductIdentityPolicy>>(
                name: "SupportedIdentityPolicies",
                table: "Products",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportedIdentityPolicies",
                table: "Products");
        }
    }
}
