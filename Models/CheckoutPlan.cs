namespace FarmGrid.Models
{
    /// <summary>
    /// The part of a checkout that becomes one Order: a single Farmer's cart items
    /// plus that Order's delivery charge.
    /// </summary>
    public sealed record FarmerOrderPlan(
        string? FarmerId,
        IReadOnlyList<CartItem> Items,
        decimal Subtotal,
        decimal DeliveryCharge)
    {
        public decimal Total => Subtotal + DeliveryCharge;
    }

    /// <summary>
    /// Splits a cart into one Order per Farmer, each delivered (and charged for
    /// delivery) separately. Cart items must have their Product loaded.
    /// </summary>
    public static class CheckoutPlan
    {
        public const decimal DeliveryChargePerOrder = 30m;

        public static IReadOnlyList<FarmerOrderPlan> Build(IEnumerable<CartItem> cartItems)
        {
            return cartItems
                .Where(c => c.Product != null)
                .GroupBy(c => c.Product!.FarmerId)
                .Select(g =>
                {
                    var items = g.ToList();
                    var subtotal = items.Sum(c => c.Product!.UnitPrice * c.Quantity);
                    return new FarmerOrderPlan(g.Key, items, subtotal, DeliveryChargePerOrder);
                })
                .ToList();
        }
    }
}
