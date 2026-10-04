using FarmGrid.Models;
using Microsoft.EntityFrameworkCore;

namespace FarmGrid.Tests
{
    public class CustomerOwnershipTests
    {
        [Theory]
        [InlineData("cart")]
        [InlineData("order")]
        [InlineData("quick-sell-order")]
        public async Task Customer_rows_must_belong_to_an_existing_user(string kind)
        {
            using var db = new TestDb();
            await using var context = db.CreateContext();

            var product = new Product { FarmerId = "farmer", Title = "Okra", Category = ProduceCategories.Vegetables, UnitMeasure = "kg", UnitPrice = 40m, StockQuantity = 10m };
            var listing = new QuickSellListing { FarmerId = "farmer", FarmerName = "Ramesh", CropTitle = "Carrots", BulkQuantity = 10m, AvailableQuantity = 10m, StartingPrice = 30m, FloorPrice = 15m };
            context.Products.Add(product);
            context.QuickSellListings.Add(listing);
            await context.SaveChangesAsync();

            switch (kind)
            {
                case "cart":
                    context.CartItems.Add(new CartItem { CustomerId = "guest-customer", ProductId = product.Id, Quantity = 1m });
                    break;
                case "order":
                    context.Orders.Add(new Order { CustomerId = "guest-customer", CustomerName = "x", PhoneNumber = "1", DeliveryAddress = "a", City = "c" });
                    break;
                case "quick-sell-order":
                    context.QuickSellOrders.Add(new QuickSellOrder { QuickSellListingId = listing.Id, CustomerId = "guest-customer", CustomerName = "x", PhoneNumber = "1", DeliveryAddress = "a", City = "c", QuantityPurchased = 1m, PricePerKg = 30m, TotalAmount = 30m });
                    break;
            }

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}
