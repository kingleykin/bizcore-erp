using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customer.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSoTien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SoTienTongHoaDon",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SoTienTrongTaiKhoan",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoTienTongHoaDon",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SoTienTrongTaiKhoan",
                table: "Customers");
        }
    }
}
