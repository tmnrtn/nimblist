using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimblist.data.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientParsedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParsedName",
                table: "RecipeIngredients",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParsedQuantity",
                table: "RecipeIngredients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParsedName",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "ParsedQuantity",
                table: "RecipeIngredients");
        }
    }
}
