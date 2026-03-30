using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickleball_Smash.Migrations
{
    /// <inheritdoc />
    public partial class RebuildSchemaToRequested : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SAN_PICKLEBALL_CHI_NHANH_ChiNhanhID",
                table: "SAN_PICKLEBALL");

            migrationBuilder.DropTable(
                name: "CHI_NHANH");

            migrationBuilder.DropTable(
                name: "CHI_TIET_DICH_VU");

            migrationBuilder.DropTable(
                name: "DICH_VU_PHU_TRO");

            migrationBuilder.DropIndex(
                name: "IX_SAN_PICKLEBALL_ChiNhanhID",
                table: "SAN_PICKLEBALL");

            migrationBuilder.DropColumn(
                name: "ChiNhanhID",
                table: "SAN_PICKLEBALL");

            migrationBuilder.AlterColumn<string>(
                name: "TrangThai",
                table: "VOUCHER",
                type: "NVARCHAR(50)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR(50)",
                oldNullable: true,
                oldDefaultValue: "Hoạt động");

            migrationBuilder.AlterColumn<string>(
                name: "TenSan",
                table: "SAN_PICKLEBALL",
                type: "NVARCHAR(100)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "NVARCHAR(100)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "BANG_GIA_KHUNG_GIO",
                columns: table => new
                {
                    MaGia = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SanID = table.Column<int>(type: "int", nullable: true),
                    GioBatDau = table.Column<TimeOnly>(type: "time", nullable: true),
                    GioKetThuc = table.Column<TimeOnly>(type: "time", nullable: true),
                    GiaTien = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BANG_GIA_KHUNG_GIO", x => x.MaGia);
                    table.ForeignKey(
                        name: "FK_BANG_GIA_KHUNG_GIO_SAN_PICKLEBALL_SanID",
                        column: x => x.SanID,
                        principalTable: "SAN_PICKLEBALL",
                        principalColumn: "SanID");
                });

            migrationBuilder.CreateTable(
                name: "HINH_ANH_SAN",
                columns: table => new
                {
                    HinhAnhID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SanID = table.Column<int>(type: "int", nullable: true),
                    DuongDanURL = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HINH_ANH_SAN", x => x.HinhAnhID);
                    table.ForeignKey(
                        name: "FK_HINH_ANH_SAN_SAN_PICKLEBALL_SanID",
                        column: x => x.SanID,
                        principalTable: "SAN_PICKLEBALL",
                        principalColumn: "SanID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BANG_GIA_KHUNG_GIO_SanID",
                table: "BANG_GIA_KHUNG_GIO",
                column: "SanID");

            migrationBuilder.CreateIndex(
                name: "IX_HINH_ANH_SAN_SanID",
                table: "HINH_ANH_SAN",
                column: "SanID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BANG_GIA_KHUNG_GIO");

            migrationBuilder.DropTable(
                name: "HINH_ANH_SAN");

            migrationBuilder.AlterColumn<string>(
                name: "TrangThai",
                table: "VOUCHER",
                type: "NVARCHAR(50)",
                nullable: true,
                defaultValue: "Hoạt động",
                oldClrType: typeof(string),
                oldType: "NVARCHAR(50)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TenSan",
                table: "SAN_PICKLEBALL",
                type: "NVARCHAR(100)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR(100)");

            migrationBuilder.AddColumn<int>(
                name: "ChiNhanhID",
                table: "SAN_PICKLEBALL",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CHI_NHANH",
                columns: table => new
                {
                    ChiNhanhID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiaChi = table.Column<string>(type: "NVARCHAR(255)", nullable: true),
                    SDT_LienHe = table.Column<string>(type: "VARCHAR(15)", nullable: true),
                    TenChiNhanh = table.Column<string>(type: "NVARCHAR(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CHI_NHANH", x => x.ChiNhanhID);
                });

            migrationBuilder.CreateTable(
                name: "DICH_VU_PHU_TRO",
                columns: table => new
                {
                    DichVuID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Gia = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LoaiDichVu = table.Column<string>(type: "NVARCHAR(50)", nullable: true),
                    TenDichVu = table.Column<string>(type: "NVARCHAR(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DICH_VU_PHU_TRO", x => x.DichVuID);
                });

            migrationBuilder.CreateTable(
                name: "CHI_TIET_DICH_VU",
                columns: table => new
                {
                    ChiTietDichVuID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DichVuID = table.Column<int>(type: "int", nullable: true),
                    DonDatSanID = table.Column<int>(type: "int", nullable: true),
                    SoLuong = table.Column<int>(type: "int", nullable: true),
                    ThanhTien = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CHI_TIET_DICH_VU", x => x.ChiTietDichVuID);
                    table.ForeignKey(
                        name: "FK_CHI_TIET_DICH_VU_DICH_VU_PHU_TRO_DichVuID",
                        column: x => x.DichVuID,
                        principalTable: "DICH_VU_PHU_TRO",
                        principalColumn: "DichVuID");
                    table.ForeignKey(
                        name: "FK_CHI_TIET_DICH_VU_DON_DAT_SAN_DonDatSanID",
                        column: x => x.DonDatSanID,
                        principalTable: "DON_DAT_SAN",
                        principalColumn: "DonDatSanID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SAN_PICKLEBALL_ChiNhanhID",
                table: "SAN_PICKLEBALL",
                column: "ChiNhanhID");

            migrationBuilder.CreateIndex(
                name: "IX_CHI_TIET_DICH_VU_DichVuID",
                table: "CHI_TIET_DICH_VU",
                column: "DichVuID");

            migrationBuilder.CreateIndex(
                name: "IX_CHI_TIET_DICH_VU_DonDatSanID",
                table: "CHI_TIET_DICH_VU",
                column: "DonDatSanID");

            migrationBuilder.AddForeignKey(
                name: "FK_SAN_PICKLEBALL_CHI_NHANH_ChiNhanhID",
                table: "SAN_PICKLEBALL",
                column: "ChiNhanhID",
                principalTable: "CHI_NHANH",
                principalColumn: "ChiNhanhID");
        }
    }
}
