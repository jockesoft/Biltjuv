using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biltjuv.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGameData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_game_data",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    money = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    respect = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    health = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    stolen_cars = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_steal_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_game_data", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_game_data_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_game_data");
        }
    }
}
