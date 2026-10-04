using FarmGrid.Controllers;
using FarmGrid.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Tests
{
    public class DisplayTests
    {
        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        [Fact]
        public void Order_numbers_have_one_format_per_order_type()
        {
            Assert.Equal("#FG0007", OrderNumbers.For(new Order { Id = 7 }));
            Assert.Equal("#QS0007", OrderNumbers.For(new QuickSellOrder { Id = 7 }));
            Assert.Equal("#FG12345", OrderNumbers.For(new Order { Id = 12345 }));
        }

        [Fact]
        public async Task Catalog_and_details_carry_the_real_farmer_name()
        {
            using var db = new TestDb();
            int id;
            await using (var context = db.CreateContext())
            {
                (await context.Users.SingleAsync(u => u.Id == "farmer")).FullName = "Ramesh Patel";
                var product = new Product { FarmerId = "farmer", Title = "Okra", Category = ProduceCategories.Vegetables, UnitMeasure = "kg", UnitPrice = 40m, StockQuantity = 10m };
                context.Products.Add(product);
                await context.SaveChangesAsync();
                id = product.Id;
            }

            await using (var context = db.CreateContext())
            {
                var controller = new ProductsController(context, TimeProvider.System);

                var catalog = Assert.IsAssignableFrom<IEnumerable<Product>>(Assert.IsType<ViewResult>(await controller.Index(null, null)).Model);
                Assert.Equal("Ramesh Patel", Assert.Single(catalog).Farmer?.FullName);

                var details = Assert.IsType<Product>(Assert.IsType<ViewResult>(await controller.Details(id)).Model);
                Assert.Equal("Ramesh Patel", details.Farmer?.FullName);
            }
        }

        [Fact]
        public async Task Trip_suggestions_carry_the_host_name()
        {
            using var db = new TestDb();
            var today = IndiaTime.Today(TimeProvider.System);
            await using (var context = db.CreateContext())
            {
                (await context.Users.SingleAsync(u => u.Id == "host")).FullName = "Kishore Bhai";
                context.TransportTrips.Add(new TransportTrip
                {
                    FarmerId = "host", DestinationMarket = "Anand APMC", DispatchDate = today.AddDays(1),
                    VehicleType = "Tata Ace", TotalVehicleCost = 3000m, HostCargoWeightKg = 200m, AvailableCapacityKg = 800m,
                    Participants = { new TransportParticipant { FarmerId = "host", CargoWeightKg = 200m, FareShare = 3000m, IsHost = true } }
                });
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                var httpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "joiner")], "test"))
                };
                var controller = new TransportController(context, TimeProvider.System)
                {
                    ControllerContext = new ControllerContext { HttpContext = httpContext },
                    TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
                };

                var result = Assert.IsType<ViewResult>(await controller.Suggestions("Anand APMC", today.AddDays(1), 50m));
                var trip = Assert.Single(Assert.IsAssignableFrom<IEnumerable<TransportTrip>>(result.Model));
                Assert.Equal("Kishore Bhai", trip.Farmer?.FullName);
            }
        }
    }
}
