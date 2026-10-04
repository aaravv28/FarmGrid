using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderFarmerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FarmerId",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            // Backfill orders whose items all come from one farmer. Orders placed
            // before checkout split by farmer may mix farmers; those stay unassigned.
            migrationBuilder.Sql("""
                UPDATE o SET FarmerId = f.FarmerId
                FROM Orders o
                JOIN (
                    SELECT oi.OrderId, MIN(p.FarmerId) AS FarmerId
                    FROM OrderItems oi
                    JOIN Products p ON p.Id = oi.ProductId
                    GROUP BY oi.OrderId
                    HAVING COUNT(DISTINCT p.FarmerId) = 1
                       AND COUNT(*) = COUNT(p.FarmerId)
                ) f ON f.OrderId = o.Id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmerId",
                table: "Orders");
        }
    }
}
