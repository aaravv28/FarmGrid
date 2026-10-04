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
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FarmGrid.Tests
{
    public class ProduceListTests
    {
        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static T SignedIn<T>(T controller, string userId) where T : Controller
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
            };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
            return controller;
        }

        private static List<ValidationResult> Validate(object model)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results;
        }

        private static ProductInputModel Product(string unit, decimal stock, string category = ProduceCategories.Vegetables) => new()
        {
            Title = "Eggs", Category = category, UnitMeasure = unit, UnitPrice = 70m, StockQuantity = stock
        };

        [Fact]
        public void Every_seeded_unit_and_category_is_one_the_forms_offer()
        {
            Assert.Contains("L", ProduceUnits.All.Select(u => u.Code));
            Assert.Contains("kg", ProduceUnits.All.Select(u => u.Code));
            Assert.Contains(ProduceCategories.DairyProducts, ProduceCategories.All);
        }

        [Theory]
        [InlineData("litre")]
        [InlineData("unit")]
        [InlineData("bunches")]
        public void Products_only_accept_known_units(string unit)
        {
            Assert.NotEmpty(Validate(Product(unit, 5m)));
        }

        [Fact]
        public void Products_only_accept_known_categories()
        {
            Assert.NotEmpty(Validate(Product("kg", 5m, category: "Dairy")));
            Assert.Empty(Validate(Product("kg", 5m, category: ProduceCategories.DairyProducts)));
        }

        [Fact]
        public void Whole_number_units_cannot_be_stocked_in_fractions()
        {
            Assert.NotEmpty(Validate(Product("dozen", 2.5m)));
            Assert.Empty(Validate(Product("dozen", 3m)));
            Assert.Empty(Validate(Product("kg", 2.5m)));
        }

        [Fact]
        public void Quick_sell_lots_use_the_same_categories()
        {
            var lot = new QuickSellCreateViewModel { CropTitle = "Milk", Category = "Dairy", BulkQuantity = 100m, StartingPrice = 60m, FloorPrice = 40m };
            Assert.NotEmpty(Validate(lot));
            lot.Category = ProduceCategories.DairyProducts;
            Assert.Empty(Validate(lot));
        }

        [Fact]
        public async Task A_whole_number_product_cannot_be_added_to_the_cart_in_fractions()
        {
            using var db = new TestDb();
            int productId;
            await using (var context = db.CreateContext())
            {
                var product = new Product { FarmerId = "farmer", Title = "Eggs", Category = ProduceCategories.DairyProducts, UnitMeasure = "dozen", UnitPrice = 70m, StockQuantity = 10m };
                context.Products.Add(product);
                await context.SaveChangesAsync();
                productId = product.Id;
            }

            await using (var context = db.CreateContext())
            {
                await SignedIn(new CartController(context, TimeProvider.System), "cust").Add(productId, 1.5m);
            }

            await using var check = db.CreateContext();
            Assert.Empty(await check.CartItems.ToListAsync());
        }

        [Fact]
        public async Task The_last_part_of_a_quick_sell_lot_can_be_bought_even_under_one_kg()
        {
            using var db = new TestDb();
            var now = DateTime.UtcNow;
            int listingId;
            await using (var context = db.CreateContext())
            {
                var listing = new QuickSellListing
                {
                    FarmerId = "farmer", FarmerName = "Ramesh", CropTitle = "Carrots",
                    BulkQuantity = 100m, AvailableQuantity = 0.6m, StartingPrice = 30m, FloorPrice = 15m,
                    CreatedAt = now.AddHours(-1), ExpiresAt = now.AddHours(47)
                };
                context.QuickSellListings.Add(listing);
                await context.SaveChangesAsync();
                listingId = listing.Id;
            }

            var purchase = new QuickSellPurchaseViewModel
            {
                ListingId = listingId, Quantity = 0.6m, ShownPricePerKg = 30m,
                CustomerName = "Priya", PhoneNumber = "9999999999", DeliveryAddress = "12 Main Rd", City = "Vadodara"
            };
            Assert.Empty(Validate(purchase));

            await using (var context = db.CreateContext())
            {
                var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context), null!,
                    new PasswordHasher<ApplicationUser>(), [], [], null!, new IdentityErrorDescriber(), null!, null!);
                await SignedIn(new QuickSellController(context, userManager, TimeProvider.System), "cust").Purchase(purchase);
            }

            await using var check = db.CreateContext();
            Assert.Equal(0.6m, (await check.QuickSellOrders.SingleAsync()).QuantityPurchased);
        }

        [Fact]
        public async Task Under_one_kg_is_refused_when_more_of_the_lot_remains()
        {
            using var db = new TestDb();
            var now = DateTime.UtcNow;
            int listingId;
            await using (var context = db.CreateContext())
            {
                var listing = new QuickSellListing
                {
                    FarmerId = "farmer", FarmerName = "Ramesh", CropTitle = "Carrots",
                    BulkQuantity = 100m, AvailableQuantity = 40m, StartingPrice = 30m, FloorPrice = 15m,
                    CreatedAt = now.AddHours(-1), ExpiresAt = now.AddHours(47)
                };
                context.QuickSellListings.Add(listing);
                await context.SaveChangesAsync();
                listingId = listing.Id;
            }

            await using (var context = db.CreateContext())
            {
                var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context), null!,
                    new PasswordHasher<ApplicationUser>(), [], [], null!, new IdentityErrorDescriber(), null!, null!);
                await SignedIn(new QuickSellController(context, userManager, TimeProvider.System), "cust").Purchase(new QuickSellPurchaseViewModel
                {
                    ListingId = listingId, Quantity = 0.5m, ShownPricePerKg = 30m,
                    CustomerName = "Priya", PhoneNumber = "9999999999", DeliveryAddress = "12 Main Rd", City = "Vadodara"
                });
            }

            await using var check = db.CreateContext();
            Assert.Empty(await check.QuickSellOrders.ToListAsync());
        }
    }
}
