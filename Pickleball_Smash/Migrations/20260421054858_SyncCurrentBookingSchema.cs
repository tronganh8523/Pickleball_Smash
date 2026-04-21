using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickleball_Smash.Migrations
{
    /// <inheritdoc />
    public partial class SyncCurrentBookingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThoiGianBatDau",
                table: "DON_DAT_SAN");

            migrationBuilder.DropColumn(
                name: "ThoiGianKetThuc",
                table: "DON_DAT_SAN");

            migrationBuilder.AddColumn<string>(
                name: "KhungGio",
                table: "DON_DAT_SAN",
                type: "NVARCHAR(255)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KhungGio",
                table: "DON_DAT_SAN");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ThoiGianBatDau",
                table: "DON_DAT_SAN",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ThoiGianKetThuc",
                table: "DON_DAT_SAN",
                type: "time",
                nullable: true);
        }
    }
}
