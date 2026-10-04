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
    public class TripCorrectnessTests
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
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, farmerId)], "test"))
            };
            return new TransportController(context, TimeProvider.System)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
            };
        }

        private static DateTime Today => IndiaTime.Today(TimeProvider.System);

        private static TransportTrip Trip(DateTime dispatchDate) => new()
        {
            FarmerId = "host", DestinationMarket = "Central Mandi", DispatchDate = dispatchDate,
            VehicleType = "Tata Ace", TotalVehicleCost = 3000m, HostCargoWeightKg = 200m, AvailableCapacityKg = 800m,
            Participants = { new TransportParticipant { FarmerId = "host", CargoWeightKg = 200m, FareShare = 3000m, IsHost = true } }
        };

        [Fact]
        public void Fare_shares_add_up_to_exactly_the_vehicle_cost()
        {
            var trip = new TransportTrip
            {
                TotalVehicleCost = 1000m,
                Participants =
                {
                    new TransportParticipant { FarmerId = "host", CargoWeightKg = 100m, IsHost = true },
                    new TransportParticipant { FarmerId = "a", CargoWeightKg = 100m },
                    new TransportParticipant { FarmerId = "b", CargoWeightKg = 100m }
                }
            };

            trip.RecalculateFareShares();

            Assert.Equal(1000m, trip.Participants.Sum(p => p.FareShare));
            Assert.All(trip.Participants, p => Assert.Equal(p.FareShare, decimal.Round(p.FareShare, 2)));
            Assert.Equal(333.34m, trip.Participants.Single(p => p.IsHost).FareShare);
        }

        [Fact]
        public async Task A_trip_cannot_be_scheduled_in_the_past()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                var result = await ControllerFor(context, "host").Create(new TripInputModel
                {
                    DestinationMarket = "Central Mandi", DispatchDate = Today.AddDays(-1), VehicleType = "Tata Ace",
                    TotalVehicleCost = 3000m, HostCargoWeightKg = 200m, AvailableCapacityKg = 800m
                });
                Assert.IsType<ViewResult>(result);
            }

            await using var check = db.CreateContext();
            Assert.Empty(await check.TransportTrips.ToListAsync());
        }

        [Fact]
        public async Task Suggestions_leave_out_trips_the_farmer_already_joined()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                var joined = Trip(Today.AddDays(1));
                joined.Participants.Add(new TransportParticipant { FarmerId = "joiner", CargoWeightKg = 100m });
                context.TransportTrips.AddRange(joined, Trip(Today.AddDays(1)));
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                var result = await ControllerFor(context, "joiner").Suggestions("Central Mandi", Today.AddDays(1), 50m);
                var trips = Assert.IsAssignableFrom<IEnumerable<TransportTrip>>(Assert.IsType<ViewResult>(result).Model);
                var trip = Assert.Single(trips);
                Assert.DoesNotContain(trip.Participants, p => p.FarmerId == "joiner");
            }
        }

        [Fact]
        public async Task Searching_for_a_past_date_is_refused()
        {
            using var db = new TestDb();
            await using var context = db.CreateContext();
            var controller = ControllerFor(context, "joiner");

            var result = await controller.Suggestions("Central Mandi", Today.AddDays(-2), 50m);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task Nearby_date_results_are_flagged_as_such()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                context.TransportTrips.Add(Trip(Today.AddDays(3)));
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                var result = Assert.IsType<ViewResult>(await ControllerFor(context, "joiner").Suggestions("Central Mandi", Today.AddDays(1), 50m));
                Assert.Single(Assert.IsAssignableFrom<IEnumerable<TransportTrip>>(result.Model));
                Assert.True((bool?)result.ViewData["IsNearbyDateMatch"]);
            }
        }
    }
}
