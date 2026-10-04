using FarmGrid.Controllers;
using FarmGrid.Data;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using System.Security.Claims;
using System.Text.Json;

namespace FarmGrid.Tests
{
    public class QuickSellPriceTests
    {
        private static readonly DateTime Now = new(2026, 10, 4, 6, 0, 0, DateTimeKind.Utc);

        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static QuickSellController ControllerFor(ApplicationDbContext context)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "cust")], "test"))
            };
            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context), null!,
                new PasswordHasher<ApplicationUser>(), [], [], null!, new IdentityErrorDescriber(), null!, null!);
            return new QuickSellController(context, userManager, new FakeTimeProvider(new DateTimeOffset(Now)))
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
            };
        }

        /// <summary>A lot 24h into its 48h window: price is exactly halfway, ₹22.50.</summary>
        private static async Task<int> SeedHalfwayLot(TestDb db)
        {
            await using var context = db.CreateContext();
            context.Users.Add(new ApplicationUser { Id = "cust", UserName = "c@x.com", FullName = "Priya" });
            var listing = new QuickSellListing
            {
                FarmerId = "farmer", FarmerName = "Ramesh", CropTitle = "Carrots",
                BulkQuantity = 100m, AvailableQuantity = 100m, StartingPrice = 30m, FloorPrice = 15m,
                CreatedAt = Now.AddHours(-24), ExpiresAt = Now.AddHours(24)
            };
            context.QuickSellListings.Add(listing);
            await context.SaveChangesAsync();
            return listing.Id;
        }

        private static QuickSellPurchaseViewModel Purchase(int listingId, decimal shownPrice) => new()
        {
            ListingId = listingId, Quantity = 10m, ShownPricePerKg = shownPrice,
            CustomerName = "Priya", PhoneNumber = "9999999999", DeliveryAddress = "12 Main Rd", City = "Vadodara"
        };

        [Fact]
        public async Task A_purchase_is_charged_the_current_price_when_it_is_at_or_below_the_shown_price()
        {
            using var db = new TestDb();
            var listingId = await SeedHalfwayLot(db);

            await using (var context = db.CreateContext())
            {
                await ControllerFor(context).Purchase(Purchase(listingId, shownPrice: 22.60m));
            }

            await using var check = db.CreateContext();
            var order = await check.QuickSellOrders.SingleAsync();
            Assert.Equal(22.5m, order.PricePerKg);
            Assert.Equal(225m, order.TotalAmount);
        }

        [Fact]
        public async Task A_purchase_is_refused_if_the_current_price_is_higher_than_the_customer_was_shown()
        {
            using var db = new TestDb();
            var listingId = await SeedHalfwayLot(db);

            await using (var context = db.CreateContext())
            {
                var controller = ControllerFor(context);
                await controller.Purchase(Purchase(listingId, shownPrice: 20m));
                Assert.Contains("₹22.50", controller.TempData["Error"]?.ToString());
            }

            await using var check = db.CreateContext();
            Assert.Empty(await check.QuickSellOrders.ToListAsync());
            Assert.Equal(100m, (await check.QuickSellListings.SingleAsync()).AvailableQuantity);
        }

        [Fact]
        public void Prices_round_half_away_from_zero()
        {
            var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            // 10.48 -> 10.00 over 48h drops exactly ₹0.01 an hour; at 1.5h the exact price is 10.465
            var listing = new QuickSellListing
            {
                StartingPrice = 10.48m, FloorPrice = 10.00m, DurationHours = 48,
                CreatedAt = created, ExpiresAt = created.AddHours(48)
            };

            Assert.Equal(10.47m, listing.CalculateCurrentPrice(created.AddHours(1.5)));
        }

        [Fact]
        public async Task Live_price_api_reports_server_price_and_remaining_seconds()
        {
            using var db = new TestDb();
            var listingId = await SeedHalfwayLot(db);

            await using var context = db.CreateContext();
            var result = Assert.IsType<JsonResult>(await ControllerFor(context).GetLivePrice(listingId));
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));

            Assert.Equal(22.5m, json.RootElement.GetProperty("currentPrice").GetDecimal());
            Assert.Equal(24 * 3600, json.RootElement.GetProperty("remainingSeconds").GetInt32());
            Assert.True(json.RootElement.GetProperty("isBuyable").GetBoolean());
        }
    }
}
