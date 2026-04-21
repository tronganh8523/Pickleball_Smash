using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickleball_Smash.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceBangGiaTimeWithKhungGio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GioBatDau",
                table: "BANG_GIA_KHUNG_GIO");

            migrationBuilder.DropColumn(
                name: "GioKetThuc",
                table: "BANG_GIA_KHUNG_GIO");

            migrationBuilder.AddColumn<string>(
                name: "KhungGio",
                table: "BANG_GIA_KHUNG_GIO",
                type: "NVARCHAR(255)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KhungGio",
                table: "BANG_GIA_KHUNG_GIO");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "GioBatDau",
                table: "BANG_GIA_KHUNG_GIO",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "GioKetThuc",
                table: "BANG_GIA_KHUNG_GIO",
                type: "time",
                nullable: true);
        }
    }
}
