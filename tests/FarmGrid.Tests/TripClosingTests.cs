using FarmGrid.Controllers;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Tests
{
    public class TripClosingTests
    {
        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static TransportController ControllerFor(FarmGrid.Data.ApplicationDbContext context, string farmerId)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, farmerId)], "test"))
            };
            return new TransportController(context, TimeProvider.System)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
            };
        }

        private static TransportTrip Trip(DateTime dispatchDate, string market = "Anand APMC") => new()
        {
            FarmerId = "host",
            DestinationMarket = market,
            DispatchDate = dispatchDate,
            VehicleType = "Tata Ace",
            TotalVehicleCost = 3000m,
            HostCargoWeightKg = 200m,
            AvailableCapacityKg = 800m,
            Participants = { new TransportParticipant { FarmerId = "host", CargoWeightKg = 200m, FareShare = 3000m, IsHost = true } }
        };

        [Fact]
        public void A_trip_is_open_through_its_dispatch_date_and_closed_after()
        {
            var today = new DateTime(2026, 10, 4);
            Assert.True(Trip(today.AddDays(1)).IsOpen(today));
            Assert.True(Trip(today.AddHours(6)).IsOpen(today));
            Assert.False(Trip(today.AddDays(-1)).IsOpen(today));
            Assert.False(new TransportTrip { DispatchDate = today.AddDays(1), IsActive = false }.IsOpen(today));
        }

        [Fact]
        public async Task Joining_a_trip_whose_dispatch_date_has_passed_is_rejected()
        {
            using var db = new TestDb();
            int tripId;
            await using (var context = db.CreateContext())
            {
                var trip = Trip(IndiaTime.Today(TimeProvider.System).AddDays(-1));
                context.TransportTrips.Add(trip);
                await context.SaveChangesAsync();
                tripId = trip.Id;
            }

            await using (var context = db.CreateContext())
            {
                await ControllerFor(context, "joiner").Join(new TransportJoinViewModel { TripId = tripId, CargoWeightKg = 100m });
            }

            await using (var context = db.CreateContext())
            {
                var trip = await context.TransportTrips.Include(t => t.Participants).SingleAsync();
                Assert.Single(trip.Participants);
                Assert.Equal(800m, trip.AvailableCapacityKg);
            }
        }

        [Fact]
        public async Task Suggestions_never_offer_a_closed_trip_even_for_its_exact_date()
        {
            using var db = new TestDb();
            var yesterday = IndiaTime.Today(TimeProvider.System).AddDays(-1);
            await using (var context = db.CreateContext())
            {
                context.TransportTrips.Add(Trip(yesterday));
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                // A past date is refused before searching, so the closed trip is never listed
                var result = await ControllerFor(context, "joiner").Suggestions("Anand APMC", yesterday, 100m);
                Assert.IsType<RedirectToActionResult>(result);
            }
        }

        [Fact]
        public async Task Transport_hub_lists_only_open_trips()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                context.TransportTrips.AddRange(Trip(IndiaTime.Today(TimeProvider.System).AddDays(-1), "Closed Market"), Trip(IndiaTime.Today(TimeProvider.System).AddDays(2), "Open Market"));
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                var result = await ControllerFor(context, "joiner").Index();
                var trips = Assert.IsAssignableFrom<IEnumerable<TransportTrip>>(Assert.IsType<ViewResult>(result).Model);
                Assert.Equal(new[] { "Open Market" }, trips.Select(t => t.DestinationMarket));
            }
        }
    }
}
