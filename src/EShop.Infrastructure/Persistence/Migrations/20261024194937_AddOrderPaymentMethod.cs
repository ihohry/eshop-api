using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Prepaid");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Courier_Prepaid",
                table: "Orders",
                sql: "\"DeliveryMethod\" = 'PickupPoint' OR \"PaymentMethod\" = 'Prepaid'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status_PaymentMethod",
                table: "Orders",
                sql: "\"PaymentMethod\" = 'PayOnReceipt' OR \"Status\" NOT IN ('PendingConfirmation', 'Confirmed', 'Refused')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Courier_Prepaid",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status_PaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Orders");
        }
    }
}
