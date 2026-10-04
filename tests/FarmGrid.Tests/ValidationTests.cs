using FarmGrid.Controllers;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;

namespace FarmGrid.Tests
{
    public class ValidationTests
    {
        /// <summary>Form field → the entity column it is saved to.</summary>
        public static TheoryData<Type, string, Type, string> FormFieldsSavedToLimitedColumns => new()
        {
            { typeof(QuickSellPurchaseViewModel), nameof(QuickSellPurchaseViewModel.CustomerName), typeof(QuickSellOrder), nameof(QuickSellOrder.CustomerName) },
            { typeof(QuickSellPurchaseViewModel), nameof(QuickSellPurchaseViewModel.PhoneNumber), typeof(QuickSellOrder), nameof(QuickSellOrder.PhoneNumber) },
            { typeof(QuickSellPurchaseViewModel), nameof(QuickSellPurchaseViewModel.DeliveryAddress), typeof(QuickSellOrder), nameof(QuickSellOrder.DeliveryAddress) },
            { typeof(QuickSellPurchaseViewModel), nameof(QuickSellPurchaseViewModel.City), typeof(QuickSellOrder), nameof(QuickSellOrder.City) },
            { typeof(QuickSellCreateViewModel), nameof(QuickSellCreateViewModel.CropTitle), typeof(QuickSellListing), nameof(QuickSellListing.CropTitle) },
            { typeof(QuickSellCreateViewModel), nameof(QuickSellCreateViewModel.Category), typeof(QuickSellListing), nameof(QuickSellListing.Category) },
            { typeof(QuickSellCreateViewModel), nameof(QuickSellCreateViewModel.Location), typeof(QuickSellListing), nameof(QuickSellListing.Location) },
            { typeof(QuickSellCreateViewModel), nameof(QuickSellCreateViewModel.Description), typeof(QuickSellListing), nameof(QuickSellListing.Description) },
            { typeof(ProductInputModel), nameof(ProductInputModel.Title), typeof(Product), nameof(Product.Title) },
            { typeof(ProductInputModel), nameof(ProductInputModel.Description), typeof(Product), nameof(Product.Description) },
            { typeof(RegisterViewModel), nameof(RegisterViewModel.FullName), typeof(ApplicationUser), nameof(ApplicationUser.FullName) },
            { typeof(RegisterViewModel), nameof(RegisterViewModel.City), typeof(ApplicationUser), nameof(ApplicationUser.City) },
            { typeof(RegisterViewModel), nameof(RegisterViewModel.District), typeof(ApplicationUser), nameof(ApplicationUser.District) },
            { typeof(ProfileViewModel), nameof(ProfileViewModel.FullName), typeof(ApplicationUser), nameof(ApplicationUser.FullName) },
            { typeof(ProfileViewModel), nameof(ProfileViewModel.City), typeof(ApplicationUser), nameof(ApplicationUser.City) },
            { typeof(ProfileViewModel), nameof(ProfileViewModel.District), typeof(ApplicationUser), nameof(ApplicationUser.District) },
        };

        private static int? MaxLengthOf(PropertyInfo property) =>
            property.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength
            ?? property.GetCustomAttribute<MaxLengthAttribute>()?.Length;

        [Theory]
        [MemberData(nameof(FormFieldsSavedToLimitedColumns))]
        public void Form_fields_are_limited_to_the_length_their_column_can_hold(Type form, string field, Type entity, string column)
        {
            using var db = new TestDb();
            using var context = db.CreateContext();
            var columnLimit = context.Model.FindEntityType(entity)!.FindProperty(column)!.GetMaxLength();
            Assert.NotNull(columnLimit);

            var formLimit = MaxLengthOf(form.GetProperty(field)!);
            Assert.True(formLimit != null && formLimit <= columnLimit,
                $"{form.Name}.{field} allows {formLimit?.ToString() ?? "unlimited"} characters but {entity.Name}.{column} holds {columnLimit}.");
        }

        [Fact]
        public void Checkout_fields_have_sensible_limits()
        {
            foreach (var field in new[] { nameof(CheckoutViewModel.CustomerName), nameof(CheckoutViewModel.PhoneNumber), nameof(CheckoutViewModel.DeliveryAddress), nameof(CheckoutViewModel.City) })
            {
                Assert.NotNull(MaxLengthOf(typeof(CheckoutViewModel).GetProperty(field)!));
            }
        }

        [Fact]
        public async Task Checkout_names_the_product_that_is_no_longer_available()
        {
            using var db = new TestDb();
            await using (var context = db.CreateContext())
            {
                var product = new Product { FarmerId = "farmer", Title = "Alphonso Mangoes", Category = "Fruits", UnitMeasure = "kg", UnitPrice = 120m, StockQuantity = 10m, IsActive = false };
                context.Products.Add(product);
                await context.SaveChangesAsync();
                context.CartItems.Add(new CartItem { CustomerId = "cust", ProductId = product.Id, Quantity = 2m });
                await context.SaveChangesAsync();
            }

            await using (var context = db.CreateContext())
            {
                var controller = new OrdersController(context, null!, TimeProvider.System)
                {
                    ControllerContext = new ControllerContext
                    {
                        HttpContext = new DefaultHttpContext
                        {
                            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "cust")], "test"))
                        }
                    }
                };

                var result = await controller.Checkout(new CheckoutViewModel
                {
                    CustomerName = "Priya", PhoneNumber = "9999999999", DeliveryAddress = "12 Main Rd", City = "Vadodara"
                });

                Assert.IsType<ViewResult>(result);
                var message = Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage;
                Assert.Contains("Alphonso Mangoes", message);
            }
        }

        [Fact]
        public void Checkout_problems_customers_should_see_use_a_dedicated_exception()
        {
            var ex = new CheckoutProblemException("Insufficient stock for Okra.");
            Assert.IsAssignableFrom<Exception>(ex);
            Assert.Equal("Insufficient stock for Okra.", ex.Message);
        }
    }
}
