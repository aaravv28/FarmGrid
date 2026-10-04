using FarmGrid.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FarmGrid.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Ensure Database & Migrations are applied
            await context.Database.MigrateAsync();

            // Remove leftover users and data from the retired B2B role
            await LegacyB2BCleanup.RunAsync(context);

            // 2. Seed Identity Roles
            foreach (var role in Roles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 3. Seed Demo Users
            var demoUsers = new[]
            {
                new
                {
                    Email = "farmer@farmgrid.com",
                    Password = "Farmer@123",
                    FullName = "Ramesh Patel (Demo Farmer)",
                    Phone = "+91 98765 43210",
                    City = "Anand",
                    District = "Gujarat",
                    Role = Roles.Farmer
                },
                new
                {
                    Email = "customer@farmgrid.com",
                    Password = "Customer@123",
                    FullName = "Priya Sharma (Demo Customer)",
                    Phone = "+91 98123 45678",
                    City = "Vadodara",
                    District = "Gujarat",
                    Role = Roles.Customer
                }
            };

            string farmerUserId = string.Empty;

            foreach (var u in demoUsers)
            {
                var existingUser = await userManager.FindByEmailAsync(u.Email);
                if (existingUser == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = u.Email,
                        Email = u.Email,
                        FullName = u.FullName,
                        PhoneNumber = u.Phone,
                        City = u.City,
                        District = u.District,
                        EmailConfirmed = true,
                        CreatedAt = DateTime.Now
                    };

                    var result = await userManager.CreateAsync(user, u.Password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, u.Role);
                        if (u.Role == Roles.Farmer)
                        {
                            farmerUserId = user.Id;
                        }
                    }
                }
                else if (u.Role == Roles.Farmer)
                {
                    farmerUserId = existingUser.Id;
                }
            }

            // Fallback farmer ID if existing accounts are used
            if (string.IsNullOrEmpty(farmerUserId))
            {
                var anyFarmer = (await userManager.GetUsersInRoleAsync(Roles.Farmer)).FirstOrDefault();
                farmerUserId = anyFarmer?.Id ?? "demo-farmer-id";
            }

            // 4. Seed Regular Marketplace Products
            if (!await context.Products.AnyAsync())
            {
                var sampleProducts = new List<Product>
                {
                    new Product
                    {
                        FarmerId = farmerUserId,
                        Title = "Organic Fresh Red Tomatoes",
                        Category = "Vegetables",
                        UnitMeasure = "kg",
                        UnitPrice = 28.00m,
                        StockQuantity = 120,
                        Description = "Naturally vine-ripened red hybrid tomatoes, harvested daily without synthetic pesticides.",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    },
                    new Product
                    {
                        FarmerId = farmerUserId,
                        Title = "Farm Fresh Potatoes",
                        Category = "Vegetables",
                        UnitMeasure = "kg",
                        UnitPrice = 22.00m,
                        StockQuantity = 350,
                        Description = "Locally grown clean dirt-free yellow potatoes, perfect for daily domestic and commercial cooking.",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    },
                    new Product
                    {
                        FarmerId = farmerUserId,
                        Title = "Shimla Red Royal Apples",
                        Category = "Fruits",
                        UnitMeasure = "kg",
                        UnitPrice = 140.00m,
                        StockQuantity = 150,
                        Description = "Crisp, sweet, and juicy handpicked mountain apples from high-altitude orchards.",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    },
                    new Product
                    {
                        FarmerId = farmerUserId,
                        Title = "Nagpur Sweet Oranges",
                        Category = "Fruits",
                        UnitMeasure = "kg",
                        UnitPrice = 65.00m,
                        StockQuantity = 200,
                        Description = "Rich in Vitamin C, naturally sweet and tangy fresh citrus oranges directly from farmer groves.",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    },
                    new Product
                    {
                        FarmerId = farmerUserId,
                        Title = "Pure Desi Cow Bilona Ghee",
                        Category = "Dairy Products",
                        UnitMeasure = "kg",
                        UnitPrice = 850.00m,
                        StockQuantity = 45,
                        Description = "Hand-churned A2 desi cow ghee prepared through traditional curd-bilona wood-fire processing.",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    },
                    new Product
                    {
                        FarmerId = farmerUserId,
                        Title = "Fresh Farm Buffalo Milk",
                        Category = "Dairy Products",
                        UnitMeasure = "L",
                        UnitPrice = 65.00m,
                        StockQuantity = 80,
                        Description = "100% pure raw whole buffalo milk with high natural fat content, chilled and packed fresh.",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    },
                    new Product
                    {
                        FarmerId = farmerUserId,
                        Title = "Aromatic Premium Basmati Rice",
                        Category = "Groceries",
                        UnitMeasure = "kg",
                        UnitPrice = 95.00m,
                        StockQuantity = 500,
                        Description = "Aged extra-long grain fragrant basmati rice directly processed from local paddy farmers.",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    },
                    new Product
                    {
                        FarmerId = farmerUserId,
                        Title = "Organic Whole Sharbati Wheat",
                        Category = "Groceries",
                        UnitMeasure = "kg",
                        UnitPrice = 42.00m,
                        StockQuantity = 800,
                        Description = "Golden heavy-kernel Sharbati wheat grains, cleaned and graded for wholesome soft rotis.",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    }
                };

                context.Products.AddRange(sampleProducts);
                await context.SaveChangesAsync();
            }

            // 5. Seed QuickSell Listings
            if (!await context.QuickSellListings.AnyAsync())
            {
                var now = DateTime.Now;
                var sampleQuickSells = new List<QuickSellListing>
                {
                    new QuickSellListing
                    {
                        FarmerId = farmerUserId,
                        FarmerName = "Ramesh Patel",
                        Location = "Anand, Gujarat",
                        CropTitle = "Fresh Tomatoes",
                        Category = "Vegetables",
                        UnitMeasure = "kg",
                        BulkQuantity = 500,
                        AvailableQuantity = 350,
                        StartingPrice = 30.00m,
                        FloorPrice = 15.00m,
                        DurationHours = 48,
                        CreatedAt = now.AddHours(-18),
                        ExpiresAt = now.AddHours(30),
                        IsActive = true,
                        Description = "High-grade organic ripe hybrid tomatoes, harvested this morning. Needs fast clearing."
                    },
                    new QuickSellListing
                    {
                        FarmerId = farmerUserId,
                        FarmerName = "Ramesh Patel",
                        Location = "Kheda, Gujarat",
                        CropTitle = "Orange Carrots",
                        Category = "Vegetables",
                        UnitMeasure = "kg",
                        BulkQuantity = 800,
                        AvailableQuantity = 620,
                        StartingPrice = 45.00m,
                        FloorPrice = 25.00m,
                        DurationHours = 48,
                        CreatedAt = now.AddHours(-12),
                        ExpiresAt = now.AddHours(36),
                        IsActive = true,
                        Description = "Fresh crunchy orange carrots directly sorted from field. Ideal for processing or home use."
                    },
                    new QuickSellListing
                    {
                        FarmerId = farmerUserId,
                        FarmerName = "Ramesh Patel",
                        Location = "Vadodara, Gujarat",
                        CropTitle = "Alphonso & Kesar Mangoes",
                        Category = "Fruits",
                        UnitMeasure = "kg",
                        BulkQuantity = 400,
                        AvailableQuantity = 400,
                        StartingPrice = 120.00m,
                        FloorPrice = 75.00m,
                        DurationHours = 48,
                        CreatedAt = now.AddHours(-6),
                        ExpiresAt = now.AddHours(42),
                        IsActive = true,
                        Description = "Naturally ripened sweet mango crates. Perfect commercial grade sweetness."
                    }
                };

                context.QuickSellListings.AddRange(sampleQuickSells);
                await context.SaveChangesAsync();
            }

            // 6. Seed Shared Transport Trips
            if (!await context.TransportTrips.AnyAsync())
            {
                var today = DateTime.Today;

                var trip1 = new TransportTrip
                {
                    FarmerId = farmerUserId,
                    DestinationMarket = "Central Mandi",
                    DispatchDate = today.AddDays(1),
                    VehicleType = "Tata Ace 1.5 Ton",
                    TotalVehicleCost = 2800.00m,
                    HostCargoWeightKg = 600.00m,
                    AvailableCapacityKg = 900.00m,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                var trip2 = new TransportTrip
                {
                    FarmerId = farmerUserId,
                    DestinationMarket = "City Hub",
                    DispatchDate = today.AddDays(2),
                    VehicleType = "Mahindra Bolero Maxi",
                    TotalVehicleCost = 4200.00m,
                    HostCargoWeightKg = 1200.00m,
                    AvailableCapacityKg = 1300.00m,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                var trip3 = new TransportTrip
                {
                    FarmerId = farmerUserId,
                    DestinationMarket = "Wholesale Market",
                    DispatchDate = today.AddDays(3),
                    VehicleType = "Eicher Pro 2049",
                    TotalVehicleCost = 6500.00m,
                    HostCargoWeightKg = 2500.00m,
                    AvailableCapacityKg = 2000.00m,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                context.TransportTrips.AddRange(trip1, trip2, trip3);
                await context.SaveChangesAsync();

                // Add Host participants with initial fare calculation
                context.TransportParticipants.AddRange(
                    new TransportParticipant
                    {
                        TransportTripId = trip1.Id,
                        FarmerId = farmerUserId,
                        CargoWeightKg = trip1.HostCargoWeightKg,
                        FareShare = trip1.TotalVehicleCost,
                        IsHost = true,
                        JoinedAt = DateTime.Now
                    },
                    new TransportParticipant
                    {
                        TransportTripId = trip2.Id,
                        FarmerId = farmerUserId,
                        CargoWeightKg = trip2.HostCargoWeightKg,
                        FareShare = trip2.TotalVehicleCost,
                        IsHost = true,
                        JoinedAt = DateTime.Now
                    },
                    new TransportParticipant
                    {
                        TransportTripId = trip3.Id,
                        FarmerId = farmerUserId,
                        CargoWeightKg = trip3.HostCargoWeightKg,
                        FareShare = trip3.TotalVehicleCost,
                        IsHost = true,
                        JoinedAt = DateTime.Now
                    }
                );

                await context.SaveChangesAsync();
            }
        }
    }
}
