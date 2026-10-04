using FarmGrid.Controllers;
using FarmGrid.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Tests
{
    public class CartCheckTests
    {
        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static CartItem Line(decimal quantity, decimal stock, bool isActive = true, string unit = "kg") => new()
        {
            Quantity = quantity,
            Product = new Product { Title = "Okra", UnitMeasure = unit, UnitPrice = 40m, StockQuantity = stock, IsActive = isActive }
        };

        [Fact]
        public void A_line_that_can_be_bought_has_no_problem()
        {
            Assert.Null(CartChecks.ProblemWith(Line(2m, 10m)));
        }

        [Fact]
        public void Deleted_sold_out_short_and_fractional_lines_are_flagged_by_name()
        {
            Assert.Contains("Okra is no longer available", CartChecks.ProblemWith(Line(2m, 10m, isActive: false)));
            Assert.Contains("Okra is no longer available", CartChecks.ProblemWith(Line(2m, 0m)));
            Assert.Contains("Only 1 kg of Okra is left", CartChecks.ProblemWith(Line(2m, 1m)));
            Assert.Contains("whole number", CartChecks.ProblemWith(Line(1.5m, 10m, unit: "dozen")));
        }

        [Fact]
        public async Task Checkout_sends_the_customer_back_to_the_cart_while_a_line_has_a_problem()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                var gone = new Product { FarmerId = "farmer", Title = "Alphonso Mangoes", Category = ProduceCategories.Fruits, UnitMeasure = "kg", UnitPrice = 120m, StockQuantity = 5m, IsActive = false };
                context.Products.Add(gone);
                await context.SaveChangesAsync();
                context.CartItems.Add(new CartItem { CustomerId = "cust", ProductId = gone.Id, Quantity = 1m });
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                var httpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "cust")], "test"))
                };
                var controller = new OrdersController(context, null!, TimeProvider.System)
                {
                    ControllerContext = new ControllerContext { HttpContext = httpContext },
                    TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
                };

                var result = Assert.IsType<RedirectToActionResult>(await controller.Checkout());

                Assert.Equal("Cart", result.ControllerName);
                Assert.Contains("Alphonso Mangoes", controller.TempData["Error"]?.ToString());
            }
        }
    }
}
