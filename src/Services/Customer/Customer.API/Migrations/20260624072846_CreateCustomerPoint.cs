using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customer.API.Migrations
{
    /// <inheritdoc />
    public partial class CreateCustomerPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerPoint",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerPoint",
                table: "Customers");
        }
    }
}
