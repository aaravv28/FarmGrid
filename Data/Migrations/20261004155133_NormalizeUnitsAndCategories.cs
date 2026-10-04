using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUnitsAndCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One list of units and categories (Models/Produce.cs). The product form used to
            // offer "litre" and "unit" while seeds used "L"; Quick Sell offered "Dairy".
            migrationBuilder.Sql("UPDATE Products SET UnitMeasure = 'L' WHERE UnitMeasure IN ('litre', 'liter', 'ltr');");
            migrationBuilder.Sql("UPDATE Products SET UnitMeasure = 'piece' WHERE UnitMeasure IN ('unit', 'units', 'pcs', 'cobs');");
            migrationBuilder.Sql("UPDATE Products SET UnitMeasure = 'bunch' WHERE UnitMeasure = 'bunches';");
            migrationBuilder.Sql("UPDATE OrderItems SET UnitMeasure = 'bunch' WHERE UnitMeasure = 'bunches';");
            migrationBuilder.Sql("UPDATE OrderItems SET UnitMeasure = 'L' WHERE UnitMeasure IN ('litre', 'liter', 'ltr');");
            migrationBuilder.Sql("UPDATE OrderItems SET UnitMeasure = 'piece' WHERE UnitMeasure IN ('unit', 'units', 'pcs', 'cobs');");
            migrationBuilder.Sql("UPDATE Products SET Category = 'Dairy Products' WHERE Category = 'Dairy';");
            migrationBuilder.Sql("UPDATE QuickSellListings SET Category = 'Dairy Products' WHERE Category = 'Dairy';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration; previous spellings are not restored.
        }
    }
}
