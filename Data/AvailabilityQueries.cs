using FarmGrid.Models;

namespace FarmGrid.Data
{
    /// <summary>
    /// The single definition of what customers can see and buy.
    /// IsActive means only "deleted by the farmer"; selling out never changes it.
    /// </summary>
    public static class AvailabilityQueries
    {
        /// <summary>Catalog products that are listed and in stock (mirrors <see cref="Product.IsAvailable"/>).</summary>
        public static IQueryable<Product> Available(this IQueryable<Product> products)
        {
            return products.Where(p => p.IsActive && p.StockQuantity > 0);
        }

        /// <summary>Quick Sell lots that are listed, have quantity left and have not expired (mirrors <see cref="QuickSellListing.IsBuyable"/>).</summary>
        public static IQueryable<QuickSellListing> Buyable(this IQueryable<QuickSellListing> listings, DateTime now)
        {
            return listings.Where(q => q.IsActive && q.AvailableQuantity > 0 && q.ExpiresAt > now);
        }
    }
}
