using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestration.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIdToPaymentSagaState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                table: "PaymentSagaStates",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "PaymentSagaStates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSagaStates_OrderId",
                table: "PaymentSagaStates",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentSagaStates_OrderId",
                table: "PaymentSagaStates");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "PaymentSagaStates");

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                table: "PaymentSagaStates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
