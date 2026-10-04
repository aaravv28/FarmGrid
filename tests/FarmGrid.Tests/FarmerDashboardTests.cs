using FarmGrid.Controllers;
using FarmGrid.Data;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Tests
{
    public class FarmerDashboardTests
    {
        private static UserManager<ApplicationUser> UserManagerFor(ApplicationDbContext context) =>
            new(new UserStore<ApplicationUser>(context), null!, new PasswordHasher<ApplicationUser>(),
                [], [], null!, new IdentityErrorDescriber(), null!, null!);

        private static async Task<FarmerDashboardViewModel> DashboardFor(TestDb db, string farmerId)
        {
            await using var context = db.CreateContext();
            var controller = new UiController(context, UserManagerFor(context))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, farmerId)], "test"))
                    }
                }
            };
            var result = await controller.FarmerDashboard();
            return Assert.IsType<FarmerDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);
        }

        [Fact]
        public async Task A_new_farmer_sees_an_empty_dashboard_not_other_farmers_data()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                context.Users.AddRange(
                    new ApplicationUser { Id = "veteran", UserName = "v@x.com", FullName = "Ramesh", City = "Anand", District = "Gujarat" },
                    new ApplicationUser { Id = "newbie", UserName = "n@x.com", FullName = "Suresh" });

                var product = new Product { FarmerId = "veteran", Title = "Tomatoes", Category = "Vegetables", UnitMeasure = "kg", UnitPrice = 30m, StockQuantity = 10m };
                var listing = new QuickSellListing { FarmerId = "veteran", FarmerName = "Ramesh", CropTitle = "Carrots", BulkQuantity = 100m, AvailableQuantity = 60m, StartingPrice = 30m, FloorPrice = 15m };
                context.Products.Add(product);
                context.QuickSellListings.Add(listing);
                context.TransportTrips.Add(new TransportTrip { FarmerId = "veteran", DestinationMarket = "Central Mandi", DispatchDate = DateTime.Today.AddDays(1), VehicleType = "Tata Ace", TotalVehicleCost = 3000m, HostCargoWeightKg = 100m, AvailableCapacityKg = 500m });
                await context.SaveChangesAsync();

                context.QuickSellOrders.Add(new QuickSellOrder { QuickSellListingId = listing.Id, CustomerId = "cust", CustomerName = "Priya", PhoneNumber = "1", DeliveryAddress = "a", City = "Vadodara", QuantityPurchased = 40m, PricePerKg = 20m, TotalAmount = 800m });
                await context.SaveChangesAsync();
            }

            var dashboard = await DashboardFor(db, "newbie");

            Assert.Empty(dashboard.MyProducts);
            Assert.Empty(dashboard.MyQuickSells);
            Assert.Empty(dashboard.RecentQuickSellOrders);
            Assert.Empty(dashboard.RecentRetailOrders);
            Assert.Empty(dashboard.MyTransportTrips);
            Assert.Equal(0m, dashboard.TotalEarnings);
            Assert.True(string.IsNullOrEmpty(dashboard.Location));

            var veteran = await DashboardFor(db, "veteran");
            Assert.Single(veteran.MyProducts);
            Assert.Equal(800m, veteran.TotalEarnings);
            Assert.Equal("Anand, Gujarat", veteran.Location);
        }
    }
}
