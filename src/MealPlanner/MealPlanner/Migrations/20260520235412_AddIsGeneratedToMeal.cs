using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanner.Migrations
{
    /// <inheritdoc />
    public partial class AddIsGeneratedToMeal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guard against the column already existing (e.g. applied manually or via a prior run).
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE Name = N'IsGenerated'
                      AND Object_ID = Object_ID(N'Meal')
                )
                BEGIN
                    ALTER TABLE [Meal] ADD [IsGenerated] bit NOT NULL DEFAULT CAST(0 AS bit)
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGenerated",
                table: "Meal");
        }
    }
}
