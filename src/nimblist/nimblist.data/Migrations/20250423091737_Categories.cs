using System;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimblist.data.Migrations
{
    /// <inheritdoc />
    public partial class Categories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubCategoryId",
                table: "Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubCategories_Categories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_CategoryId",
                table: "Items",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_SubCategoryId",
                table: "Items",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_ParentCategoryId",
                table: "SubCategories",
                column: "ParentCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Categories_CategoryId",
                table: "Items",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_SubCategories_SubCategoryId",
                table: "Items",
                column: "SubCategoryId",
                principalTable: "SubCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Load data from categories.csv and subcategories.csv
            LoadCategoryData(migrationBuilder);

        }

        // Load data from categories.csv
        private void LoadCategoryData(MigrationBuilder migrationBuilder)
        {
            var categoriesCsvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "categories.csv");
            Console.WriteLine(categoriesCsvPath);
            if (File.Exists(categoriesCsvPath))
            {
                Console.WriteLine($"Loading categories from {categoriesCsvPath}");
                var categories = File.ReadAllLines(categoriesCsvPath);
                foreach (var line in categories.Skip(1)) // Skip header
                {
                    // Use regex to properly handle CSV with potential quoted fields
                    var parts = ParseCsvLine(line);
                    if (parts.Length >= 2)
                    {
                        var id = parts[0];
                        var name = parts[1].Replace("'", "''"); // Escape single quotes for SQL
                        migrationBuilder.Sql($"INSERT INTO \"Categories\" (\"Id\", \"Name\") VALUES ('{id}', '{name}')");
                    }
                }
            }

            // Load data from subcategories.csv
            var subcategoriesCsvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "subcategories.csv");
            Console.WriteLine(subcategoriesCsvPath);
            if (File.Exists(subcategoriesCsvPath))
            {
                Console.WriteLine($"Loading subcategories from {subcategoriesCsvPath}");
                var subcategories = File.ReadAllLines(subcategoriesCsvPath);
                foreach (var line in subcategories.Skip(1)) // Skip header
                {
                    var parts = ParseCsvLine(line);
                    if (parts.Length >= 3)
                    {
                        var id = parts[0];
                        var name = parts[1].Replace("'", "''"); // Escape single quotes
                        var parentCategoryId = parts[2];
                        migrationBuilder.Sql($"INSERT INTO \"SubCategories\" (\"Id\", \"Name\", \"ParentCategoryId\") VALUES ('{id}', '{name}', '{parentCategoryId}')");
                    }
                }
            }
        }

        private string[] ParseCsvLine(string line)
        {
            List<string> result = new List<string>();
            bool inQuotes = false;
            StringBuilder field = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // If this is a double quote within a quoted field (escaped quote)
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        field.Append('"');
                        i++; // Skip the next quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(field.ToString());
                    field.Clear();
                }
                else
                {
                    field.Append(c);
                }
            }

            // Add the last field
            result.Add(field.ToString());

            return result.ToArray();
        }
        

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Categories_CategoryId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_SubCategories_SubCategoryId",
                table: "Items");

            migrationBuilder.DropTable(
                name: "SubCategories");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Items_CategoryId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_SubCategoryId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SubCategoryId",
                table: "Items");
        }
    }
}
