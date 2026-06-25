using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customer.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_CustomerGroups_CustomerGroupId1",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CustomerGroupId1",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerGroupId1",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerGroupId",
                table: "Customers");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerGroupId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerGroupId",
                table: "Customers",
                column: "CustomerGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_CustomerGroups_CustomerGroupId",
                table: "Customers",
                column: "CustomerGroupId",
                principalTable: "CustomerGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_CustomerGroups_CustomerGroupId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CustomerGroupId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerGroupId",
                table: "Customers");

            migrationBuilder.AddColumn<int>(
                name: "CustomerGroupId",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerGroupId1",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerGroupId1",
                table: "Customers",
                column: "CustomerGroupId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_CustomerGroups_CustomerGroupId1",
                table: "Customers",
                column: "CustomerGroupId1",
                principalTable: "CustomerGroups",
                principalColumn: "Id");
        }
    }
}
