using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biltjuv.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseIdToUserGameData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "warehouse_id",
                table: "user_game_data",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "warehouse_id",
                table: "user_game_data");
        }
    }
}
