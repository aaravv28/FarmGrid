using FarmGrid.Controllers;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Tests
{
    public class SplitCheckoutTests
    {
        private static Product NewProduct(string farmerId, string title, decimal price) => new()
        {
            FarmerId = farmerId,
            Title = title,
            Category = "Vegetables",
            UnitMeasure = "kg",
            UnitPrice = price,
            StockQuantity = 100m
        };

        [Fact]
        public void Plan_groups_cart_by_farmer_with_one_delivery_charge_each()
        {
            var a1 = new CartItem { Quantity = 2m, Product = NewProduct("farmer-a", "Tomatoes", 30m) };
            var a2 = new CartItem { Quantity = 1m, Product = NewProduct("farmer-a", "Onions", 40m) };
            var b1 = new CartItem { Quantity = 3m, Product = NewProduct("farmer-b", "Milk", 60m) };

            var plan = CheckoutPlan.Build([a1, b1, a2]);

            Assert.Equal(2, plan.Count);
            var a = plan.Single(p => p.FarmerId == "farmer-a");
            var b = plan.Single(p => p.FarmerId == "farmer-b");
            Assert.Equal(100m, a.Subtotal);
            Assert.Equal(180m, b.Subtotal);
            Assert.All(plan, p => Assert.Equal(CheckoutPlan.DeliveryChargePerOrder, p.DeliveryCharge));
            Assert.Equal(130m, a.Total);
            Assert.Equal(new[] { a1, a2 }, a.Items);
        }

        [Fact]
        public async Task Checkout_of_a_two_farmer_cart_places_one_order_per_farmer()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                var tomatoes = NewProduct("farmer-a", "Tomatoes", 30m);
                var onions = NewProduct("farmer-a", "Onions", 40m);
                var milk = NewProduct("farmer-b", "Milk", 60m);
                context.Products.AddRange(tomatoes, onions, milk);
                await context.SaveChangesAsync();
                context.CartItems.AddRange(
                    new CartItem { CustomerId = "cust", ProductId = tomatoes.Id, Quantity = 2m },
                    new CartItem { CustomerId = "cust", ProductId = onions.Id, Quantity = 1m },
                    new CartItem { CustomerId = "cust", ProductId = milk.Id, Quantity = 3m });
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                var controller = new OrdersController(context, null!)
                {
                    ControllerContext = new ControllerContext
                    {
                        HttpContext = new DefaultHttpContext
                        {
                            User = new ClaimsPrincipal(new ClaimsIdentity(
                                [new Claim(ClaimTypes.NameIdentifier, "cust")], "test"))
                        }
                    }
                };
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
                var orders = await context.Orders.Include(o => o.OrderItems).ToListAsync();
                Assert.Equal(2, orders.Count);

                var a = orders.Single(o => o.FarmerId == "farmer-a");
                var b = orders.Single(o => o.FarmerId == "farmer-b");
                Assert.Equal(100m, a.Subtotal);
                Assert.Equal(180m, b.Subtotal);
                Assert.Equal(60m, orders.Sum(o => o.DeliveryCharge));
                Assert.Equal(130m, a.TotalAmount);
                Assert.Equal(210m, b.TotalAmount);
                Assert.Equal(new[] { "Onions", "Tomatoes" }, a.OrderItems.Select(i => i.ProductTitle).OrderBy(t => t));
                Assert.Equal(new[] { "Milk" }, b.OrderItems.Select(i => i.ProductTitle));
                Assert.All(orders, o => Assert.Equal("cust", o.CustomerId));

                Assert.Empty(await context.CartItems.ToListAsync());
            }
        }
    }
}
