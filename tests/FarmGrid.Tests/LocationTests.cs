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
    public class LocationTests
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

        private static RegisterViewModel Registration(string? city) => new()
        {
            FullName = "Suresh", Email = "s@x.com", PhoneNumber = "9999999999", Role = Roles.Farmer,
            City = city, Password = "Secret@123", ConfirmPassword = "Secret@123"
        };

        [Fact]
        public void Gujarat_has_34_districts_each_with_cities_and_every_city_name_is_unique()
        {
            Assert.Equal(34, GujaratLocations.Districts.Length);
            Assert.Contains("Vav-Tharad", GujaratLocations.Districts);
            Assert.All(GujaratLocations.Districts, d => Assert.NotEmpty(GujaratLocations.CitiesIn(d)));
            Assert.Equal(GujaratLocations.Cities.Length, GujaratLocations.Cities.Select(c => c.Name).Distinct().Count());
            Assert.InRange(GujaratLocations.Cities.Length, 60, 100);
        }

        [Theory]
        [InlineData("Unjha", "Mehsana")]
        [InlineData("Nadiad", "Kheda")]
        [InlineData("Tharad", "Vav-Tharad")]
        [InlineData("Anand", "Anand")]
        public void A_city_determines_its_district(string city, string district)
        {
            Assert.Equal(district, GujaratLocations.DistrictOf(city));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Mumbai")]
        [InlineData("anand")]
        public void Registration_requires_a_listed_gujarat_city(string? city)
        {
            Assert.NotEmpty(Validate(Registration(city)));
        }

        [Fact]
        public void Registration_accepts_a_listed_city()
        {
            Assert.Empty(Validate(Registration("Gondal")));
        }

        [Fact]
        public async Task Saving_a_profile_takes_the_district_from_the_city_whatever_was_posted()
        {
            using var db = new TestDb();
            await using var context = db.CreateContext();
            var users = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context), null!,
                new PasswordHasher<ApplicationUser>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, null!);
            var controller = SignedIn(new AccountController(users, null!, null!, TimeProvider.System), "farmer");

            await controller.Profile(new ProfileViewModel
            {
                FullName = "Ramesh", Email = "farmer@farmgrid.test", City = "Unjha", District = "Surat"
            });

            var saved = await context.Users.SingleAsync(u => u.Id == "farmer");
            Assert.Equal("Unjha", saved.City);
            Assert.Equal("Mehsana", saved.District);
        }

        [Fact]
        public void A_trips_origin_is_the_hosts_current_location()
        {
            var host = new ApplicationUser { City = "Anand", District = "Anand" };
            var trip = new TransportTrip { Farmer = host };
            Assert.Equal("Anand, Anand district", trip.OriginDescription);

            host.City = "Gondal";
            host.District = "Rajkot";
            Assert.Equal("Gondal, Rajkot district", trip.OriginDescription);
        }

        [Fact]
        public async Task Suggestions_list_trips_from_the_searchers_own_district_first()
        {
            using var db = new TestDb();
            var today = IndiaTime.Today(TimeProvider.System);
            await using (var context = db.CreateContext())
            {
                var users = await context.Users.ToDictionaryAsync(u => u.Id);
                (users["joiner"].City, users["joiner"].District) = ("Nadiad", "Kheda");
                (users["host"].City, users["host"].District) = ("Surat", "Surat");
                (users["other"].City, users["other"].District) = ("Kapadvanj", "Kheda");

                TransportTrip Trip(string hostId, decimal cost) => new()
                {
                    FarmerId = hostId, DestinationMarket = Markets.Anand.Name, DispatchDate = today.AddDays(1),
                    VehicleType = "Tata Ace", TotalVehicleCost = cost, HostCargoWeightKg = 100m, AvailableCapacityKg = 500m,
                    Participants = { new TransportParticipant { FarmerId = hostId, CargoWeightKg = 100m, FareShare = cost, IsHost = true } }
                };

                // The far-away trip is cheaper, so cost alone would list it first
                context.TransportTrips.AddRange(Trip("host", 1000m), Trip("other", 3000m));
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                var result = Assert.IsType<ViewResult>(await SignedIn(new TransportController(context, TimeProvider.System), "joiner")
                    .Suggestions(Markets.Anand.Name, today.AddDays(1), 50m));
                var trips = Assert.IsAssignableFrom<IEnumerable<TransportTrip>>(result.Model).ToList();

                Assert.Equal(new[] { "other", "host" }, trips.Select(t => t.FarmerId));
            }
        }
    }
}
