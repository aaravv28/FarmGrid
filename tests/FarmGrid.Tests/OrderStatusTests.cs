using FarmGrid.Controllers;
using FarmGrid.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Security.Claims;

namespace FarmGrid.Tests
{
    public class OrderStatusTests
    {
        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static FarmerOrdersController ControllerFor(FarmGrid.Data.ApplicationDbContext context, string farmerId)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, farmerId)], "test"))
            };
            return new FarmerOrdersController(context)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
            };
        }

        private static async Task<(int OrderId, int ProductId)> SeedRetailOrder(TestDb db, string farmerId = "farmer-a", string status = OrderStatuses.Placed)
        {
            await using var context = db.CreateContext();
            var product = new Product { FarmerId = farmerId, Title = "Tomatoes", Category = "Vegetables", UnitMeasure = "kg", UnitPrice = 30m, StockQuantity = 8m };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var order = new Order
            {
                CustomerId = "cust",
                FarmerId = farmerId,
                CustomerName = "Priya",
                PhoneNumber = "1",
                DeliveryAddress = "a",
                City = "c",
                Status = status,
                OrderItems = { new OrderItem { ProductId = product.Id, ProductTitle = "Tomatoes", UnitMeasure = "kg", Quantity = 2m, UnitPrice = 30m, TotalPrice = 60m } }
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            return (order.Id, product.Id);
        }

        private static async Task<(int OrderId, int ListingId)> SeedQuickSellOrder(TestDb db, bool expired = false)
        {
            await using var context = db.CreateContext();
            var now = DateTime.UtcNow;
            var listing = new QuickSellListing
            {
                FarmerId = "farmer-a",
                FarmerName = "Ramesh",
                CropTitle = "Carrots",
                BulkQuantity = 100m,
                AvailableQuantity = 60m,
                StartingPrice = 30m,
                FloorPrice = 15m,
                CreatedAt = expired ? now.AddHours(-60) : now.AddHours(-1),
                ExpiresAt = expired ? now.AddHours(-12) : now.AddHours(47)
            };
            context.QuickSellListings.Add(listing);
            await context.SaveChangesAsync();

            var order = new QuickSellOrder
            {
                QuickSellListingId = listing.Id,
                CustomerId = "cust",
                CustomerName = "Priya",
                PhoneNumber = "1",
                DeliveryAddress = "a",
                City = "c",
                QuantityPurchased = 40m,
                PricePerKg = 20m,
                TotalAmount = 800m
            };
            context.QuickSellOrders.Add(order);
            await context.SaveChangesAsync();
            return (order.Id, listing.Id);
        }

        [Fact]
        public void New_orders_start_as_placed()
        {
            Assert.Equal(OrderStatuses.Placed, new Order().Status);
            Assert.Equal(OrderStatuses.Placed, new QuickSellOrder().Status);
        }

        [Fact]
        public async Task Owning_farmer_marks_an_order_delivered()
        {
            using var db = new TestDb();
            var (orderId, _) = await SeedRetailOrder(db);

            await using (var context = db.CreateContext())
            {
                Assert.IsType<RedirectToActionResult>(await ControllerFor(context, "farmer-a").Deliver(orderId));
            }

            await using (var context = db.CreateContext())
            {
                Assert.Equal(OrderStatuses.Delivered, (await context.Orders.SingleAsync()).Status);
            }
        }

        [Fact]
        public async Task Owning_farmer_cancelling_an_order_returns_its_stock()
        {
            using var db = new TestDb();
            var (orderId, productId) = await SeedRetailOrder(db);

            await using (var context = db.CreateContext())
            {
                await ControllerFor(context, "farmer-a").Cancel(orderId);
            }

            await using (var context = db.CreateContext())
            {
                Assert.Equal(OrderStatuses.Cancelled, (await context.Orders.SingleAsync()).Status);
                Assert.Equal(10m, (await context.Products.SingleAsync(p => p.Id == productId)).StockQuantity);
            }
        }

        [Fact]
        public async Task Another_farmer_cannot_change_the_order()
        {
            using var db = new TestDb();
            var (orderId, productId) = await SeedRetailOrder(db);

            await using (var context = db.CreateContext())
            {
                Assert.IsType<NotFoundResult>(await ControllerFor(context, "farmer-b").Cancel(orderId));
                Assert.IsType<NotFoundResult>(await ControllerFor(context, "farmer-b").Deliver(orderId));
            }

            await using (var context = db.CreateContext())
            {
                Assert.Equal(OrderStatuses.Placed, (await context.Orders.SingleAsync()).Status);
                Assert.Equal(8m, (await context.Products.SingleAsync(p => p.Id == productId)).StockQuantity);
            }
        }

        [Theory]
        [InlineData(OrderStatuses.Delivered)]
        [InlineData(OrderStatuses.Cancelled)]
        public async Task Delivered_and_cancelled_are_final(string finalStatus)
        {
            using var db = new TestDb();
            var (orderId, productId) = await SeedRetailOrder(db, status: finalStatus);

            await using (var context = db.CreateContext())
            {
                await ControllerFor(context, "farmer-a").Cancel(orderId);
                await ControllerFor(context, "farmer-a").Deliver(orderId);
            }

            await using (var context = db.CreateContext())
            {
                Assert.Equal(finalStatus, (await context.Orders.SingleAsync()).Status);
                Assert.Equal(8m, (await context.Products.SingleAsync(p => p.Id == productId)).StockQuantity);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Cancelling_a_quick_sell_order_returns_quantity_to_the_lot_even_after_expiry(bool expired)
        {
            using var db = new TestDb();
            var (orderId, listingId) = await SeedQuickSellOrder(db, expired);

            await using (var context = db.CreateContext())
            {
                await ControllerFor(context, "farmer-a").CancelQuickSell(orderId);
            }

            await using (var context = db.CreateContext())
            {
                Assert.Equal(OrderStatuses.Cancelled, (await context.QuickSellOrders.SingleAsync()).Status);
                Assert.Equal(100m, (await context.QuickSellListings.SingleAsync(l => l.Id == listingId)).AvailableQuantity);
            }
        }

        [Fact]
        public async Task Owning_farmer_delivers_a_quick_sell_order_and_others_cannot()
        {
            using var db = new TestDb();
            var (orderId, _) = await SeedQuickSellOrder(db);

            await using (var context = db.CreateContext())
            {
                Assert.IsType<NotFoundResult>(await ControllerFor(context, "farmer-b").DeliverQuickSell(orderId));
                Assert.IsType<RedirectToActionResult>(await ControllerFor(context, "farmer-a").DeliverQuickSell(orderId));
            }

            await using (var context = db.CreateContext())
            {
                Assert.Equal(OrderStatuses.Delivered, (await context.QuickSellOrders.SingleAsync()).Status);
            }
        }

        [Fact]
        public void Only_farmers_can_reach_status_changes()
        {
            var authorize = typeof(FarmerOrdersController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.Equal(Roles.Farmer, authorize?.Roles);
        }

        [Fact]
        public void Cancelled_orders_do_not_count_towards_totals()
        {
            Assert.True(OrderStatuses.CountsTowardsTotals(OrderStatuses.Placed));
            Assert.True(OrderStatuses.CountsTowardsTotals(OrderStatuses.Delivered));
            Assert.False(OrderStatuses.CountsTowardsTotals(OrderStatuses.Cancelled));
        }
    }
}
