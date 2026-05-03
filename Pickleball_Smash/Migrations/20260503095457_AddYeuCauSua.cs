using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickleball_Smash.Migrations
{
    /// <inheritdoc />
    public partial class AddYeuCauSua : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NoiDungSua",
                table: "DON_DAT_SAN",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "YeuCauSua",
                table: "DON_DAT_SAN",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoiDungSua",
                table: "DON_DAT_SAN");

            migrationBuilder.DropColumn(
                name: "YeuCauSua",
                table: "DON_DAT_SAN");
        }
    }
}
