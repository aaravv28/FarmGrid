using FarmGrid.Data;
using FarmGrid.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FarmGrid.Tests
{
    public class LegacyB2BCleanupTests
    {
        private const string B2BRoleName = "B2B Buyer";

        [Fact]
        public async Task Deletes_b2b_users_with_all_their_data_and_the_role_but_keeps_customers()
        {
            using var db = new TestDb();

            await using (var context = db.CreateContext())
            {
                var b2bRole = new IdentityRole(B2BRoleName) { NormalizedName = B2BRoleName.ToUpperInvariant() };
                var customerRole = new IdentityRole(Roles.Customer) { NormalizedName = Roles.Customer.ToUpperInvariant() };
                context.Roles.AddRange(b2bRole, customerRole);

                var b2bUser = new ApplicationUser { Id = "b2b", UserName = "b2b@x.com", FullName = "FreshMart" };
                var customer = new ApplicationUser { Id = "cust", UserName = "cust@x.com", FullName = "Priya" };
                context.Users.AddRange(b2bUser, customer);
                context.UserRoles.AddRange(
                    new IdentityUserRole<string> { UserId = "b2b", RoleId = b2bRole.Id },
                    new IdentityUserRole<string> { UserId = "cust", RoleId = customerRole.Id });

                var product = new Product { Title = "Milk", Category = "Dairy Products", UnitMeasure = "L", UnitPrice = 60m, StockQuantity = 50m };
                var listing = new QuickSellListing { FarmerId = "farmer", FarmerName = "Ramesh", CropTitle = "Tomatoes", BulkQuantity = 500m, AvailableQuantity = 500m, StartingPrice = 30m, FloorPrice = 15m };
                context.Products.Add(product);
                context.QuickSellListings.Add(listing);
                await context.SaveChangesAsync();

                foreach (var userId in new[] { "b2b", "cust" })
                {
                    context.CartItems.Add(new CartItem { CustomerId = userId, ProductId = product.Id, Quantity = 1m });
                    context.Orders.Add(new Order
                    {
                        CustomerId = userId,
                        CustomerName = userId,
                        PhoneNumber = "1",
                        DeliveryAddress = "a",
                        City = "c",
                        OrderItems = { new OrderItem { ProductId = product.Id, ProductTitle = "Milk", UnitMeasure = "L", Quantity = 1m, UnitPrice = 60m, TotalPrice = 60m } }
                    });
                    context.QuickSellOrders.Add(new QuickSellOrder
                    {
                        QuickSellListingId = listing.Id,
                        BuyerId = userId,
                        BuyerName = userId,
                        BuyerPhone = "1",
                        DeliveryAddress = "a",
                        City = "c",
                        QuantityPurchased = 10m,
                        PricePerKg = 20m,
                        TotalAmount = 200m
                    });
                }
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                await LegacyB2BCleanup.RunAsync(context);
            }

            await using (var context = db.CreateContext())
            {
                Assert.Equal(new[] { "cust" }, await context.Users.Select(u => u.Id).ToListAsync());
                Assert.False(await context.Roles.AnyAsync(r => r.Name == B2BRoleName));
                Assert.True(await context.Roles.AnyAsync(r => r.Name == Roles.Customer));
                Assert.All(await context.CartItems.ToListAsync(), c => Assert.Equal("cust", c.CustomerId));
                Assert.All(await context.Orders.ToListAsync(), o => Assert.Equal("cust", o.CustomerId));
                Assert.Equal(1, await context.OrderItems.CountAsync());
                Assert.All(await context.QuickSellOrders.ToListAsync(), o => Assert.Equal("cust", o.BuyerId));
                Assert.Equal(1, await context.Orders.CountAsync());
                Assert.Equal(1, await context.QuickSellOrders.CountAsync());
            }
        }

        [Fact]
        public async Task Does_nothing_when_the_b2b_role_no_longer_exists()
        {
            using var db = new TestDb();

            await using (var context = db.CreateContext())
            {
                context.Users.Add(new ApplicationUser { Id = "cust", UserName = "cust@x.com", FullName = "Priya" });
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                await LegacyB2BCleanup.RunAsync(context);
                Assert.Equal(1, await context.Users.CountAsync());
            }
        }
    }
}
