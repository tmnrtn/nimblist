using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimblist.data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharingAndFamilyDtos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: true),
                    SharedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeShares_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeShares_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeShares_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeShares_FamilyId",
                table: "RecipeShares",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeShares_RecipeId",
                table: "RecipeShares",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeShares_UserId",
                table: "RecipeShares",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeShares");
        }
    }
}
