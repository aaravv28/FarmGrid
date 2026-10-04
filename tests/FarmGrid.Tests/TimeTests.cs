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

namespace FarmGrid.Tests
{
    /// <summary>
    /// Instants are stored in UTC from an injected TimeProvider; calendar dates use
    /// today's date in India. 19:30 UTC on 4 Oct is already 01:00 IST on 5 Oct.
    /// </summary>
    public class TimeTests
    {
        private static readonly DateTimeOffset LateEveningUtc = new(2026, 10, 4, 19, 30, 0, TimeSpan.Zero);

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

        [Fact]
        public void Today_is_the_calendar_date_in_india()
        {
            Assert.Equal(new DateTime(2026, 10, 5), IndiaTime.Today(new FakeTimeProvider(LateEveningUtc)));
            Assert.Equal(new DateTime(2026, 10, 4), IndiaTime.Today(new FakeTimeProvider(LateEveningUtc.AddHours(-14))));
        }

        [Fact]
        public void Stored_utc_instants_display_in_india_time_and_reach_javascript_as_utc()
        {
            var utc = new DateTime(2026, 10, 4, 19, 30, 0);
            Assert.Equal(new DateTime(2026, 10, 5, 1, 0, 0), utc.ToIndiaTime());
            Assert.Equal("2026-10-04T19:30:00Z", utc.ToUtcIso());
        }

        [Fact]
        public async Task A_trip_dated_yesterday_in_india_is_closed_even_while_utc_is_still_on_that_date()
        {
            using var db = new TestDb();
            int tripId;
            await using (var context = db.CreateContext())
            {
                var trip = new TransportTrip
                {
                    FarmerId = "host", DestinationMarket = "Central Mandi", DispatchDate = new DateTime(2026, 10, 4),
                    VehicleType = "Tata Ace", TotalVehicleCost = 3000m, HostCargoWeightKg = 200m, AvailableCapacityKg = 800m,
                    Participants = { new TransportParticipant { FarmerId = "host", CargoWeightKg = 200m, FareShare = 3000m, IsHost = true } }
                };
                context.TransportTrips.Add(trip);
                await context.SaveChangesAsync();
                tripId = trip.Id;
            }

            await using (var context = db.CreateContext())
            {
                var controller = SignedIn(new TransportController(context, new FakeTimeProvider(LateEveningUtc)), "joiner");
                await controller.Join(new TransportJoinViewModel { TripId = tripId, CargoWeightKg = 100m });
            }

            await using (var check = db.CreateContext())
            {
                Assert.Single((await check.TransportTrips.Include(t => t.Participants).SingleAsync()).Participants);
            }
        }

        [Fact]
        public async Task Quick_sell_purchase_charges_the_price_at_the_current_utc_instant_and_stamps_it_in_utc()
        {
            using var db = new TestDb();
            var clock = new FakeTimeProvider(LateEveningUtc);
            var nowUtc = LateEveningUtc.UtcDateTime;
            int listingId;
            await using (var context = db.CreateContext())
            {
                context.Users.Add(new ApplicationUser { Id = "cust", UserName = "c@x.com", FullName = "Priya" });
                var listing = new QuickSellListing
                {
                    FarmerId = "farmer", FarmerName = "Ramesh", CropTitle = "Carrots",
                    BulkQuantity = 100m, AvailableQuantity = 100m, StartingPrice = 30m, FloorPrice = 15m,
                    CreatedAt = nowUtc.AddHours(-24), ExpiresAt = nowUtc.AddHours(24)
                };
                context.QuickSellListings.Add(listing);
                await context.SaveChangesAsync();
                listingId = listing.Id;
            }

            await using (var context = db.CreateContext())
            {
                var controller = SignedIn(new QuickSellController(context, UserManagerFor(context), clock), "cust");
                await controller.Purchase(new QuickSellPurchaseViewModel
                {
                    ListingId = listingId, Quantity = 10m, ShownPricePerKg = 22.5m, CustomerName = "Priya",
                    PhoneNumber = "9999999999", DeliveryAddress = "12 Main Rd", City = "Vadodara"
                });
            }

            await using (var check = db.CreateContext())
            {
                var order = await check.QuickSellOrders.SingleAsync();
                Assert.Equal(22.5m, order.PricePerKg);
                Assert.Equal(nowUtc, order.PurchasedAt);
            }
        }

        [Fact]
        public async Task New_listings_and_products_are_stamped_from_the_clock_in_utc()
        {
            using var db = new TestDb();
            var clock = new FakeTimeProvider(LateEveningUtc);
            var nowUtc = LateEveningUtc.UtcDateTime;

            await using (var context = db.CreateContext())
            {
                await SignedIn(new ProductsController(context, clock), "farmer").Create(new ProductInputModel
                {
                    Title = "Okra", Category = "Vegetables", UnitMeasure = "kg", UnitPrice = 40m, StockQuantity = 5m
                });

                await SignedIn(new QuickSellController(context, UserManagerFor(context), clock), "farmer").Create(new QuickSellCreateViewModel
                {
                    CropTitle = "Carrots", Category = "Vegetables", BulkQuantity = 100m, StartingPrice = 30m, FloorPrice = 15m, Location = "Anand"
                });
            }

            await using (var check = db.CreateContext())
            {
                Assert.Equal(nowUtc, (await check.Products.SingleAsync()).CreatedAt);
                var listing = await check.QuickSellListings.SingleAsync();
                Assert.Equal(nowUtc, listing.CreatedAt);
                Assert.Equal(nowUtc.AddHours(48), listing.ExpiresAt);
            }
        }
    }
}
