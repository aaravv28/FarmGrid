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
            var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            var utcNow = time.GetUtcNow().UtcDateTime;

            // 1. Ensure Database & Migrations are applied
            await context.Database.MigrateAsync();

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
                        CreatedAt = utcNow
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

            // Demo catalog data belongs to a real farmer; without one there is nothing to seed
            if (string.IsNullOrEmpty(farmerUserId))
            {
                var anyFarmer = (await userManager.GetUsersInRoleAsync(Roles.Farmer)).FirstOrDefault();
                if (anyFarmer == null)
                {
                    return;
                }

                farmerUserId = anyFarmer.Id;
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
                        CreatedAt = utcNow
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
                        CreatedAt = utcNow
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
                        CreatedAt = utcNow
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
                        CreatedAt = utcNow
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
                        CreatedAt = utcNow
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
                        CreatedAt = utcNow
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
                        CreatedAt = utcNow
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
                        CreatedAt = utcNow
                    }
                };

                context.Products.AddRange(sampleProducts);
                await context.SaveChangesAsync();
            }

            // 5–6. Quick Sell lots and shared trips (topped up in Development so the demo never goes stale)
            var farmerName = (await userManager.FindByIdAsync(farmerUserId))?.FullName ?? "Demo Farmer";
            var isDevelopment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment();
            await DemoData.TopUpAsync(context, farmerUserId, farmerName, time, isDevelopment);
        }
    }
}
