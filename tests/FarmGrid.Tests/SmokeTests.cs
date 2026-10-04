using FarmGrid.Models;
using Microsoft.EntityFrameworkCore;

namespace FarmGrid.Tests
{
    public class SmokeTests
    {
        [Fact]
        public async Task Product_round_trips_through_the_database()
        {
            using var db = new TestDb();

            await using (var context = db.CreateContext())
            {
                context.Products.Add(new Product
                {
                    FarmerId = "farmer",
                    Title = "Tomatoes",
                    Category = "Vegetables",
                    UnitMeasure = "kg",
                    UnitPrice = 30m,
                    StockQuantity = 10m
                });
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                var product = await context.Products.SingleAsync();
                Assert.Equal("Tomatoes", product.Title);
                Assert.Equal(10m, product.StockQuantity);
            }
        }

        [Fact]
        public void Quick_sell_price_starts_at_starting_price_and_never_drops_below_floor()
        {
            var created = new DateTime(2026, 1, 1, 8, 0, 0);
            var listing = new QuickSellListing
            {
                StartingPrice = 30m,
                FloorPrice = 15m,
                DurationHours = 48,
                CreatedAt = created,
                ExpiresAt = created.AddHours(48)
            };

            Assert.Equal(30m, listing.CalculateCurrentPrice(created));
            Assert.Equal(22.5m, listing.CalculateCurrentPrice(created.AddHours(24)));
            Assert.Equal(15m, listing.CalculateCurrentPrice(created.AddHours(100)));
        }
    }
}
