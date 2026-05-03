using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickleball_Smash.Migrations
{
    /// <inheritdoc />
    public partial class AddYeuCauHuy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "YeuCauHuy",
                table: "DON_DAT_SAN",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "YeuCauHuy",
                table: "DON_DAT_SAN");
        }
    }
}
