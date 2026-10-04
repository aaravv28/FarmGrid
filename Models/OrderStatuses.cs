namespace FarmGrid.Models
{
    /// <summary>
    /// Order status for both Orders and Quick Sell orders: Placed, then either
    /// Delivered or Cancelled. Only the owning Farmer moves an order on, and
    /// Delivered and Cancelled are final.
    /// </summary>
    public static class OrderStatuses
    {
        public const string Placed = "Placed";
        public const string Delivered = "Delivered";
        public const string Cancelled = "Cancelled";

        public static bool IsFinal(string status) => status is Delivered or Cancelled;

        /// <summary>Cancelled orders are excluded from earnings and spend.</summary>
        public static bool CountsTowardsTotals(string status) => status != Cancelled;

        public static string BadgeClass(string status) => status switch
        {
            Delivered => "bg-success",
            Cancelled => "bg-danger-subtle text-danger border",
            _ => "bg-warning-subtle text-warning border"
        };

        internal static void EnsurePlaced(string status)
        {
            if (status != Placed)
            {
                throw new InvalidOrderStatusChangeException(
                    $"This order is already {status.ToLowerInvariant()} and can no longer change.");
            }
        }
    }

    public class InvalidOrderStatusChangeException(string message) : InvalidOperationException(message);
}
