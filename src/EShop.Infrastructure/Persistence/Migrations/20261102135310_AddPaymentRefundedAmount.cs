using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRefundedAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "Payments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""UPDATE "Payments" SET "RefundedAmount" = "Amount" WHERE "Status" = 'Refunded';""");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_RefundedAmount_Range",
                table: "Payments",
                sql: "\"RefundedAmount\" >= 0 AND \"RefundedAmount\" <= \"Amount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Status_RefundedAmount",
                table: "Payments",
                sql: "(\"Status\" = 'Refunded' AND \"RefundedAmount\" = \"Amount\") OR (\"Status\" = 'Paid' AND \"RefundedAmount\" < \"Amount\") OR (\"Status\" IN ('Pending', 'Expired') AND \"RefundedAmount\" = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_RefundedAmount_Range",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Status_RefundedAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "Payments");
        }
    }
}
