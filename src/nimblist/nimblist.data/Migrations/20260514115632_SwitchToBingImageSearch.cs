using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimblist.data.Migrations
{
    /// <inheritdoc />
    public partial class SwitchToBingImageSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleSearchCseId",
                table: "LlmSettings");

            migrationBuilder.RenameColumn(
                name: "GoogleSearchApiKey",
                table: "LlmSettings",
                newName: "ImageSearchApiKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageSearchApiKey",
                table: "LlmSettings",
                newName: "GoogleSearchApiKey");

            migrationBuilder.AddColumn<string>(
                name: "GoogleSearchCseId",
                table: "LlmSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
