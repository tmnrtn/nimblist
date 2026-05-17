using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimblist.data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsComplimentaryAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsComplimentaryAccess",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsComplimentaryAccess",
                table: "AspNetUsers");
        }
    }
}
