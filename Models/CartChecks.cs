namespace FarmGrid.Models
{
    /// <summary>
    /// Whether a cart line can be bought as it stands. The cart, the checkout page and
    /// placing the order all use this, so they agree on the rule and the wording.
    /// </summary>
    public static class CartChecks
    {
        /// <summary>A customer-facing description of what stops this line being bought, or null if nothing does.</summary>
        public static string? ProblemWith(CartItem item)
        {
            var product = item.Product;

            if (product == null || !product.IsAvailable)
            {
                return $"{product?.Title ?? "An item in your cart"} is no longer available. Please remove it from your cart.";
            }

            if (!ProduceUnits.AllowsQuantity(product.UnitMeasure, item.Quantity))
            {
                return $"{product.Title} is sold by the {product.UnitMeasure}; please choose a whole number.";
            }

            if (item.Quantity > product.StockQuantity)
            {
                return $"Only {product.StockQuantity:0.##} {product.UnitMeasure} of {product.Title} is left. Please reduce the quantity in your cart.";
            }

            return null;
        }
    }
}
