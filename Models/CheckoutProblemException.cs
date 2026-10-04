namespace FarmGrid.Models
{
    /// <summary>
    /// A checkout problem the customer can act on (an item became unavailable, not
    /// enough stock). Its message is safe to show; any other exception is not.
    /// </summary>
    public class CheckoutProblemException(string message) : Exception(message);
}
