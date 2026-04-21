using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickleball_Smash.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNgaySinhFromNguoiDung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NgaySinh",
                table: "NGUOI_DUNG");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NgaySinh",
                table: "NGUOI_DUNG",
                type: "datetime2",
                nullable: true);
        }
    }
}
