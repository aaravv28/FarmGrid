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
using System.Security.Claims;

namespace FarmGrid.Tests
{
    public class OwnershipTests
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
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
            };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
            return controller;
        }

        private static UserManager<ApplicationUser> UserManagerFor(ApplicationDbContext context) =>
            new(new UserStore<ApplicationUser>(context), null!, new PasswordHasher<ApplicationUser>(),
                [], [], null!, new IdentityErrorDescriber(), null!, null!);

        private static ProductInputModel Edits(int id) => new()
        {
            Id = id, Title = "Hijacked", Category = "Vegetables", UnitMeasure = "kg", UnitPrice = 1m, StockQuantity = 1m
        };

        private static async Task<int> SeedProduct(TestDb db, string farmerId = "farmer-a", bool isActive = true)
        {
            await using var context = db.CreateContext();
            var product = new Product { FarmerId = farmerId, Title = "Tomatoes", Category = "Vegetables", UnitMeasure = "kg", UnitPrice = 30m, StockQuantity = 10m, IsActive = isActive };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            return product.Id;
        }

        [Theory]
        [InlineData("product")]
        [InlineData("listing")]
        [InlineData("trip")]
        public async Task Owners_must_be_existing_users(string kind)
        {
            using var db = new TestDb();
            await using var context = db.CreateContext();

            switch (kind)
            {
                case "product":
                    context.Products.Add(new Product { FarmerId = "seed-farmer-1", Title = "x", Category = "Vegetables", UnitMeasure = "kg", UnitPrice = 1m, StockQuantity = 1m });
                    break;
                case "listing":
                    context.QuickSellListings.Add(new QuickSellListing { FarmerId = "anonymous-farmer", FarmerName = "x", CropTitle = "x", BulkQuantity = 1m, AvailableQuantity = 1m, StartingPrice = 2m, FloorPrice = 1m });
                    break;
                case "trip":
                    context.TransportTrips.Add(new TransportTrip { FarmerId = "demo-farmer-id", DestinationMarket = "x", DispatchDate = DateTime.UtcNow, VehicleType = "x", TotalVehicleCost = 1m, HostCargoWeightKg = 1m, AvailableCapacityKg = 1m });
                    break;
            }

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task Another_farmer_cannot_edit_or_delete_a_product()
        {
            using var db = new TestDb();
            var id = await SeedProduct(db);

            await using (var context = db.CreateContext())
            {
                var controller = SignedIn(new ProductsController(context, TimeProvider.System), "farmer-b");
                await controller.Edit(id, Edits(id));
                await controller.Delete(id);
            }

            await using var check = db.CreateContext();
            var product = await check.Products.SingleAsync();
            Assert.Equal("Tomatoes", product.Title);
            Assert.True(product.IsActive);
        }

        [Fact]
        public async Task A_deleted_product_cannot_be_edited_even_by_its_owner()
        {
            using var db = new TestDb();
            var id = await SeedProduct(db, isActive: false);

            await using (var context = db.CreateContext())
            {
                var controller = SignedIn(new ProductsController(context, TimeProvider.System), "farmer-a");
                Assert.IsType<RedirectToActionResult>(await controller.Edit(id));
                await controller.Edit(id, Edits(id));
            }

            await using var check = db.CreateContext();
            Assert.Equal("Tomatoes", (await check.Products.SingleAsync()).Title);
        }

        [Fact]
        public async Task The_quick_sell_marketplace_never_invents_listings()
        {
            using var db = new TestDb();

            await using (var context = db.CreateContext())
            {
                await new QuickSellController(context, UserManagerFor(context), TimeProvider.System).Index(null, null);
            }

            await using var check = db.CreateContext();
            Assert.Empty(await check.QuickSellListings.ToListAsync());
        }

        [Fact]
        public async Task A_quick_sell_lot_is_never_created_for_an_unknown_user()
        {
            using var db = new TestDb();

            await using (var context = db.CreateContext())
            {
                var controller = SignedIn(new QuickSellController(context, UserManagerFor(context), TimeProvider.System), "no-such-user");
                var result = await controller.Create(new QuickSellCreateViewModel
                {
                    CropTitle = "Carrots", Category = "Vegetables", BulkQuantity = 100m, StartingPrice = 30m, FloorPrice = 15m
                });
                Assert.IsType<ChallengeResult>(result);
            }

            await using var check = db.CreateContext();
            Assert.Empty(await check.QuickSellListings.ToListAsync());
        }
    }
}
