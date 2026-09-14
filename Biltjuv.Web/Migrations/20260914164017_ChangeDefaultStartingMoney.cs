using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biltjuv.Web.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDefaultStartingMoney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "money",
                table: "user_game_data",
                type: "bigint",
                nullable: false,
                defaultValue: 25000L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "money",
                table: "user_game_data",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 25000L);
        }
    }
}
