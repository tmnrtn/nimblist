using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimblist.data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyEntitiesv2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMember_AspNetUsers_UserId",
                table: "FamilyMember");

            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMember_Family_FamilyId",
                table: "FamilyMember");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FamilyMember",
                table: "FamilyMember");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Family",
                table: "Family");

            migrationBuilder.RenameTable(
                name: "FamilyMember",
                newName: "FamilyMembers");

            migrationBuilder.RenameTable(
                name: "Family",
                newName: "Families");

            migrationBuilder.RenameIndex(
                name: "IX_FamilyMember_FamilyId",
                table: "FamilyMembers",
                newName: "IX_FamilyMembers_FamilyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FamilyMembers",
                table: "FamilyMembers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Families",
                table: "Families",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMembers_AspNetUsers_UserId",
                table: "FamilyMembers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMembers_Families_FamilyId",
                table: "FamilyMembers",
                column: "FamilyId",
                principalTable: "Families",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMembers_AspNetUsers_UserId",
                table: "FamilyMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMembers_Families_FamilyId",
                table: "FamilyMembers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FamilyMembers",
                table: "FamilyMembers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Families",
                table: "Families");

            migrationBuilder.RenameTable(
                name: "FamilyMembers",
                newName: "FamilyMember");

            migrationBuilder.RenameTable(
                name: "Families",
                newName: "Family");

            migrationBuilder.RenameIndex(
                name: "IX_FamilyMembers_FamilyId",
                table: "FamilyMember",
                newName: "IX_FamilyMember_FamilyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FamilyMember",
                table: "FamilyMember",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Family",
                table: "Family",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMember_AspNetUsers_UserId",
                table: "FamilyMember",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMember_Family_FamilyId",
                table: "FamilyMember",
                column: "FamilyId",
                principalTable: "Family",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
