using FarmGrid.Controllers;
using FarmGrid.Data;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FarmGrid.Tests
{
    public class MarketTests
    {
        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static TripInputModel TripTo(string market) => new()
        {
            DestinationMarket = market, DispatchDate = IndiaTime.Today(TimeProvider.System).AddDays(1),
            VehicleType = "Tata Ace", TotalVehicleCost = 3000m, HostCargoWeightKg = 200m, AvailableCapacityKg = 800m
        };

        private static List<ValidationResult> Validate(object model)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results;
        }

        [Fact]
        public void Every_market_is_a_named_gujarat_apmc_with_city_and_district()
        {
            Assert.True(Markets.All.Length >= 10);
            Assert.Equal(Markets.All.Length, Markets.All.Select(m => m.Name).Distinct().Count());
            Assert.All(Markets.All, m =>
            {
                Assert.EndsWith(" APMC", m.Name);
                Assert.False(string.IsNullOrWhiteSpace(m.City));
                Assert.False(string.IsNullOrWhiteSpace(m.District));
            });
        }

        [Theory]
        [InlineData("Central Mandi")]
        [InlineData("City Hub")]
        [InlineData("Wholesale Market")]
        [InlineData("anand apmc")]
        public void Trips_can_only_go_to_a_listed_market(string market)
        {
            Assert.NotEmpty(Validate(TripTo(market)));
        }

        [Fact]
        public void A_listed_market_is_accepted()
        {
            Assert.Empty(Validate(TripTo(Markets.Anand.Name)));
        }

        [Fact]
        public async Task Searching_for_an_unlisted_market_is_refused()
        {
            using var db = new TestDb();
            await using var context = db.CreateContext();
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "joiner")], "test"))
            };
            var controller = new TransportController(context, TimeProvider.System)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
            };

            var result = await controller.Suggestions("Central Mandi", IndiaTime.Today(TimeProvider.System).AddDays(1), 50m);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task Demo_trips_go_to_listed_markets()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                await DemoData.TopUpAsync(context, "farmer", "Ramesh Patel", new FakeTimeProvider(DateTimeOffset.UtcNow), isDevelopment: true);
            }

            await using var check = db.CreateContext();
            var markets = await check.TransportTrips.Select(t => t.DestinationMarket).ToListAsync();
            Assert.NotEmpty(markets);
            Assert.All(markets, m => Assert.NotNull(Markets.Find(m)));
        }
    }
}
