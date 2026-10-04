using FarmGrid.Controllers;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Security.Claims;

namespace FarmGrid.Tests
{
    /// <summary>
    /// Create/Edit forms bind input models that expose only the fields a farmer may set,
    /// so over-posted fields (owner, flags, navigation collections) have nowhere to bind.
    /// </summary>
    public class InputBindingTests
    {
        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static T SignedIn<T>(T controller, string farmerId) where T : Controller
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, farmerId)], "test"))
            };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
            return controller;
        }

        private static string[] BindableProperties(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .Select(p => p.Name)
                .OrderBy(n => n)
                .ToArray();

        private static Type PostParameterType(Type controller, string action) =>
            controller.GetMethods()
                .Single(m => m.Name == action && m.GetCustomAttribute<HttpPostAttribute>() != null)
                .GetParameters()
                .Single(p => p.ParameterType.IsClass && p.ParameterType != typeof(string))
                .ParameterType;

        [Fact]
        public void Product_create_and_edit_bind_only_editable_product_fields()
        {
            var expected = new[] { "Category", "Description", "Id", "StockQuantity", "Title", "UnitMeasure", "UnitPrice" };

            Assert.Equal(typeof(ProductInputModel), PostParameterType(typeof(ProductsController), nameof(ProductsController.Create)));
            Assert.Equal(typeof(ProductInputModel), PostParameterType(typeof(ProductsController), nameof(ProductsController.Edit)));
            Assert.Equal(expected, BindableProperties(typeof(ProductInputModel)));
        }

        [Fact]
        public void Trip_create_binds_only_editable_trip_fields()
        {
            var expected = new[] { "AvailableCapacityKg", "DestinationMarket", "DispatchDate", "HostCargoWeightKg", "TotalVehicleCost", "VehicleType" };

            Assert.Equal(typeof(TripInputModel), PostParameterType(typeof(TransportController), nameof(TransportController.Create)));
            Assert.Equal(expected, BindableProperties(typeof(TripInputModel)));
        }

        [Fact]
        public async Task Creating_a_product_sets_owner_and_listing_from_the_server()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                var result = await SignedIn(new ProductsController(context, TimeProvider.System), "farmer-a").Create(new ProductInputModel
                {
                    Id = 999,
                    Title = "Okra",
                    Category = "Vegetables",
                    UnitMeasure = "kg",
                    UnitPrice = 40m,
                    StockQuantity = 25m
                });
                Assert.IsType<RedirectToActionResult>(result);
            }

            await using (var context = db.CreateContext())
            {
                var product = await context.Products.SingleAsync();
                Assert.NotEqual(999, product.Id);
                Assert.Equal("farmer-a", product.FarmerId);
                Assert.True(product.IsActive);
                Assert.Equal("Okra", product.Title);
                Assert.Empty(await context.OrderItems.ToListAsync());
            }
        }

        [Fact]
        public async Task Creating_a_trip_adds_exactly_the_host_as_participant()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                var result = await SignedIn(new TransportController(context, TimeProvider.System), "host").Create(new TripInputModel
                {
                    DestinationMarket = "Central Mandi",
                    DispatchDate = IndiaTime.Today(TimeProvider.System).AddDays(2),
                    VehicleType = "Tata Ace",
                    TotalVehicleCost = 3000m,
                    HostCargoWeightKg = 200m,
                    AvailableCapacityKg = 800m
                });
                Assert.IsType<RedirectToActionResult>(result);
            }

            await using (var context = db.CreateContext())
            {
                var trip = await context.TransportTrips.Include(t => t.Participants).SingleAsync();
                Assert.Equal("host", trip.FarmerId);
                var participant = Assert.Single(trip.Participants);
                Assert.Equal("host", participant.FarmerId);
                Assert.True(participant.IsHost);
                Assert.Equal(3000m, participant.FareShare);
            }
        }

        [Fact]
        public async Task Editing_a_product_updates_only_its_editable_fields()
        {
            using var db = new TestDb();
            int id;
            await using (var context = db.CreateContext())
            {
                var product = new Product { FarmerId = "farmer-a", Title = "Okra", Category = "Vegetables", UnitMeasure = "kg", UnitPrice = 40m, StockQuantity = 25m };
                context.Products.Add(product);
                await context.SaveChangesAsync();
                id = product.Id;
            }

            await using (var context = db.CreateContext())
            {
                await SignedIn(new ProductsController(context, TimeProvider.System), "farmer-a").Edit(id, new ProductInputModel
                {
                    Id = id,
                    Title = "Fresh Okra",
                    Category = "Vegetables",
                    UnitMeasure = "kg",
                    UnitPrice = 45m,
                    StockQuantity = 30m
                });
            }

            await using (var context = db.CreateContext())
            {
                var product = await context.Products.SingleAsync();
                Assert.Equal("Fresh Okra", product.Title);
                Assert.Equal(45m, product.UnitPrice);
                Assert.Equal("farmer-a", product.FarmerId);
            }
        }
    }
}
