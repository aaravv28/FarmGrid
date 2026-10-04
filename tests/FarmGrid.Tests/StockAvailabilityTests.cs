using FarmGrid.Controllers;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Tests
{
    public class StockAvailabilityTests
    {
        private static Product NewProduct(string title, decimal stock, bool isActive = true) => new()
        {
            Title = title,
            Category = "Vegetables",
            UnitMeasure = "kg",
            UnitPrice = 30m,
            StockQuantity = stock,
            IsActive = isActive,
            FarmerId = "farmer"
        };

        private static T SignedInAs<T>(T controller, string userId) where T : Controller
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
                }
            };
            return controller;
        }

        private static async Task<List<string>> CatalogTitles(TestDb db)
        {
            await using var context = db.CreateContext();
            var result = await new ProductsController(context, TimeProvider.System).Index(null, null);
            var model = Assert.IsAssignableFrom<IEnumerable<Product>>(Assert.IsType<ViewResult>(result).Model);
            return model.Select(p => p.Title).OrderBy(t => t).ToList();
        }

        [Fact]
        public async Task Catalog_shows_only_products_with_stock_that_are_not_deleted()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                context.Products.AddRange(
                    NewProduct("In stock", 5m),
                    NewProduct("Sold out", 0m),
                    NewProduct("Deleted", 5m, isActive: false));
                await context.SaveChangesAsync();
            }

            Assert.Equal(new[] { "In stock" }, await CatalogTitles(db));
        }

        [Fact]
        public async Task Product_details_are_not_found_when_sold_out()
        {
            using var db = new TestDb();
            int id;
            await using (var context = db.CreateContext())
            {
                var product = NewProduct("Sold out", 0m);
                context.Products.Add(product);
                await context.SaveChangesAsync();
                id = product.Id;
            }

            await using (var context = db.CreateContext())
            {
                Assert.IsType<NotFoundResult>(await new ProductsController(context, TimeProvider.System).Details(id));
            }
        }

        [Fact]
        public async Task Selling_out_at_checkout_keeps_the_product_listed_so_a_restock_shows_it_again()
        {
            using var db = new TestDb();
            int productId;
            await using (var context = db.CreateContext())
            {
                var product = NewProduct("Tomatoes", 2m);
                context.Products.Add(product);
                await context.SaveChangesAsync();
                productId = product.Id;
                context.CartItems.Add(new CartItem { CustomerId = "cust", ProductId = productId, Quantity = 2m });
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                var controller = SignedInAs(new OrdersController(context, null!, TimeProvider.System), "cust");
                var result = await controller.Checkout(new CheckoutViewModel
                {
                    CustomerName = "Priya",
                    PhoneNumber = "9999999999",
                    DeliveryAddress = "12 Main Rd",
                    City = "Vadodara"
                });
                Assert.IsType<RedirectToActionResult>(result);
            }

            await using (var context = db.CreateContext())
            {
                var product = await context.Products.SingleAsync();
                Assert.Equal(0m, product.StockQuantity);
                Assert.True(product.IsActive);
            }
            Assert.Empty(await CatalogTitles(db));

            await using (var context = db.CreateContext())
            {
                var product = await context.Products.SingleAsync();
                product.StockQuantity = 10m;
                await context.SaveChangesAsync();
            }
            Assert.Equal(new[] { "Tomatoes" }, await CatalogTitles(db));
        }

        [Fact]
        public void Quick_sell_lot_is_buyable_only_with_quantity_left_before_expiry()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0);
            QuickSellListing Lot(decimal available, int hoursLeft) => new()
            {
                StartingPrice = 30m,
                FloorPrice = 15m,
                AvailableQuantity = available,
                CreatedAt = now.AddHours(hoursLeft - 48),
                ExpiresAt = now.AddHours(hoursLeft)
            };

            Assert.True(Lot(10m, 5).IsBuyable(now));
            Assert.False(Lot(0m, 5).IsBuyable(now));
            Assert.False(Lot(10m, 0).IsBuyable(now));

            var soldOut = Lot(0m, 5);
            Assert.True(soldOut.IsSoldOut);
            Assert.False(soldOut.IsExpired(now));
        }

        [Fact]
        public async Task Quick_sell_marketplace_hides_sold_out_lots()
        {
            using var db = new TestDb();
            var now = DateTime.UtcNow;
            await using (var context = db.CreateContext())
            {
                QuickSellListing Lot(string title, decimal available) => new()
                {
                    FarmerId = "farmer",
                    FarmerName = "Ramesh",
                    CropTitle = title,
                    BulkQuantity = 100m,
                    AvailableQuantity = available,
                    StartingPrice = 30m,
                    FloorPrice = 15m,
                    CreatedAt = now.AddHours(-1),
                    ExpiresAt = now.AddHours(47)
                };
                context.QuickSellListings.AddRange(Lot("Has stock", 40m), Lot("Sold out", 0m));
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                var result = await new QuickSellController(context, null!, TimeProvider.System).Index(null, null);
                var model = Assert.IsAssignableFrom<IEnumerable<QuickSellListing>>(Assert.IsType<ViewResult>(result).Model);
                Assert.Equal(new[] { "Has stock" }, model.Select(l => l.CropTitle));
            }
        }
    }
}
