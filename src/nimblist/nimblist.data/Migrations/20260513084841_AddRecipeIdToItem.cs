using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimblist.data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeIdToItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecipeId",
                table: "Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_RecipeId",
                table: "Items",
                column: "RecipeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Recipes_RecipeId",
                table: "Items",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Recipes_RecipeId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_RecipeId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "RecipeId",
                table: "Items");
        }
    }
}
