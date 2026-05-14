using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimblist.data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsTemplateToShoppingList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "ShoppingLists",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "ShoppingLists");
        }
    }
}
