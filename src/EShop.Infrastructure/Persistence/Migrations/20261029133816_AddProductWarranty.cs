using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductWarranty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarrantyMonths",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Warranty_Range",
                table: "Products",
                sql: "\"WarrantyMonths\" BETWEEN 0 AND 120");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Warranty_Range",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "WarrantyMonths",
                table: "Products");
        }
    }
}
