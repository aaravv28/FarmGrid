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
    /// <summary>
    /// Each race is simulated deterministically: the controller's context has already
    /// read the row (as a request does just before saving), then a second context — the
    /// competing request — changes it. The controller must not overwrite that change.
    /// </summary>
    public class ConcurrencyTests
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

        [Fact]
        public async Task Two_customers_cannot_both_buy_the_last_of_a_quick_sell_lot()
        {
            using var db = new TestDb();
            int listingId;
            await using (var setup = db.CreateContext())
            {
                setup.Users.Add(new ApplicationUser { Id = "cust-1", UserName = "c1@x.com", FullName = "Priya" });
                var listing = new QuickSellListing
                {
                    FarmerId = "farmer", FarmerName = "Ramesh", CropTitle = "Carrots",
                    BulkQuantity = 100m, AvailableQuantity = 100m, StartingPrice = 30m, FloorPrice = 15m,
                    CreatedAt = DateTime.UtcNow.AddHours(-1), ExpiresAt = DateTime.UtcNow.AddHours(47)
                };
                setup.QuickSellListings.Add(listing);
                await setup.SaveChangesAsync();
                listingId = listing.Id;
            }

            await using var context = db.CreateContext();
            await context.QuickSellListings.SingleAsync(l => l.Id == listingId); // request 1 has read 100 kg

            await using (var competitor = db.CreateContext())
            {
                var listing = await competitor.QuickSellListings.SingleAsync();
                listing.AvailableQuantity -= 80m; // request 2 buys 80 kg first
                await competitor.SaveChangesAsync();
            }

            var controller = SignedIn(new QuickSellController(context, UserManagerFor(context), TimeProvider.System), "cust-1");
            await controller.Purchase(new QuickSellPurchaseViewModel
            {
                ListingId = listingId, Quantity = 80m, ShownPricePerKg = 30m, CustomerName = "Priya",
                PhoneNumber = "9999999999", DeliveryAddress = "12 Main Rd", City = "Vadodara"
            });

            await using var check = db.CreateContext();
            Assert.Equal(20m, (await check.QuickSellListings.SingleAsync()).AvailableQuantity);
            Assert.Empty(await check.QuickSellOrders.ToListAsync());
        }

        [Fact]
        public async Task Checkout_does_not_oversell_stock_taken_by_a_concurrent_checkout()
        {
            using var db = new TestDb();
            int productId;
            await using (var setup = db.CreateContext())
            {
                var product = new Product { FarmerId = "farmer", Title = "Tomatoes", Category = "Vegetables", UnitMeasure = "kg", UnitPrice = 30m, StockQuantity = 10m };
                setup.Products.Add(product);
                await setup.SaveChangesAsync();
                productId = product.Id;
                setup.CartItems.Add(new CartItem { CustomerId = "cust", ProductId = productId, Quantity = 6m });
                await setup.SaveChangesAsync();
            }

            await using var context = db.CreateContext();
            await context.Products.SingleAsync(); // request 1 has read 10 in stock

            await using (var competitor = db.CreateContext())
            {
                (await competitor.Products.SingleAsync()).StockQuantity = 4m; // request 2 took 6
                await competitor.SaveChangesAsync();
            }

            var result = await SignedIn(new OrdersController(context, null!, TimeProvider.System), "cust").Checkout(new CheckoutViewModel
            {
                CustomerName = "Priya", PhoneNumber = "9999999999", DeliveryAddress = "12 Main Rd", City = "Vadodara"
            });

            Assert.IsType<ViewResult>(result);
            await using var check = db.CreateContext();
            Assert.Equal(4m, (await check.Products.SingleAsync()).StockQuantity);
            Assert.Empty(await check.Orders.ToListAsync());
            Assert.Single(await check.CartItems.ToListAsync());
        }

        [Fact]
        public async Task Joining_a_trip_cannot_overbook_capacity_taken_concurrently()
        {
            using var db = new TestDb();
            int tripId;
            await using (var setup = db.CreateContext())
            {
                var trip = new TransportTrip
                {
                    FarmerId = "host", DestinationMarket = "Central Mandi", DispatchDate = IndiaTime.Today(TimeProvider.System).AddDays(2),
                    VehicleType = "Tata Ace", TotalVehicleCost = 3000m, HostCargoWeightKg = 200m, AvailableCapacityKg = 800m,
                    Participants = { new TransportParticipant { FarmerId = "host", CargoWeightKg = 200m, FareShare = 3000m, IsHost = true } }
                };
                setup.TransportTrips.Add(trip);
                await setup.SaveChangesAsync();
                tripId = trip.Id;
            }

            await using var context = db.CreateContext();
            await context.TransportTrips.Include(t => t.Participants).SingleAsync(); // request 1 has read 800 kg free

            await using (var competitor = db.CreateContext())
            {
                var trip = await competitor.TransportTrips.SingleAsync();
                competitor.TransportParticipants.Add(new TransportParticipant { TransportTripId = trip.Id, FarmerId = "other", CargoWeightKg = 700m });
                trip.AvailableCapacityKg -= 700m; // request 2 booked 700 kg
                await competitor.SaveChangesAsync();
            }

            await SignedIn(new TransportController(context, TimeProvider.System), "joiner").Join(new TransportJoinViewModel { TripId = tripId, CargoWeightKg = 500m });

            await using var check = db.CreateContext();
            var saved = await check.TransportTrips.Include(t => t.Participants).SingleAsync();
            Assert.Equal(100m, saved.AvailableCapacityKg);
            Assert.DoesNotContain(saved.Participants, p => p.FarmerId == "joiner");
        }

        [Fact]
        public async Task A_farmer_cannot_appear_twice_on_the_same_trip()
        {
            using var db = new TestDb();
            await using var context = db.CreateContext();
            var trip = new TransportTrip
            {
                FarmerId = "host", DestinationMarket = "Central Mandi", DispatchDate = IndiaTime.Today(TimeProvider.System).AddDays(2),
                VehicleType = "Tata Ace", TotalVehicleCost = 3000m, HostCargoWeightKg = 200m, AvailableCapacityKg = 800m
            };
            context.TransportTrips.Add(trip);
            await context.SaveChangesAsync();

            context.TransportParticipants.AddRange(
                new TransportParticipant { TransportTripId = trip.Id, FarmerId = "joiner", CargoWeightKg = 100m },
                new TransportParticipant { TransportTripId = trip.Id, FarmerId = "joiner", CargoWeightKg = 100m });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task An_order_cancelled_concurrently_returns_its_stock_only_once_and_keeps_later_sales()
        {
            using var db = new TestDb();
            int orderId;
            await using (var setup = db.CreateContext())
            {
                var product = new Product { FarmerId = "farmer", Title = "Tomatoes", Category = "Vegetables", UnitMeasure = "kg", UnitPrice = 30m, StockQuantity = 8m };
                setup.Products.Add(product);
                await setup.SaveChangesAsync();
                var order = new Order
                {
                    CustomerId = "cust", FarmerId = "farmer", CustomerName = "Priya", PhoneNumber = "1", DeliveryAddress = "a", City = "c",
                    OrderItems = { new OrderItem { ProductId = product.Id, ProductTitle = "Tomatoes", UnitMeasure = "kg", Quantity = 2m, UnitPrice = 30m, TotalPrice = 60m } }
                };
                setup.Orders.Add(order);
                await setup.SaveChangesAsync();
                orderId = order.Id;
            }

            await using var context = db.CreateContext();
            await context.Orders.Include(o => o.OrderItems).ThenInclude(i => i.Product).SingleAsync(); // request 1 sees Placed

            await using (var competitor = db.CreateContext())
            {
                var order = await competitor.Orders.Include(o => o.OrderItems).ThenInclude(i => i.Product).SingleAsync();
                order.Cancel(); // request 2 cancels first (stock 8 -> 10)
                await competitor.SaveChangesAsync();
                order.OrderItems.Single().Product!.StockQuantity -= 3m; // then 3 kg sell (10 -> 7)
                await competitor.SaveChangesAsync();
            }

            await SignedIn(new FarmerOrdersController(context), "farmer").Cancel(orderId);

            await using var check = db.CreateContext();
            Assert.Equal(7m, (await check.Products.SingleAsync()).StockQuantity);
            Assert.Equal(OrderStatuses.Cancelled, (await check.Orders.SingleAsync()).Status);
        }
    }
}
