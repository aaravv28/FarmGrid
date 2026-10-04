namespace FarmGrid.Models
{
    /// <summary>How orders are numbered wherever they are shown: #FG0001 for catalog orders, #QS0001 for Quick Sell orders.</summary>
    public static class OrderNumbers
    {
        public static string For(Order order) => $"#FG{order.Id:D4}";

        public static string For(QuickSellOrder order) => $"#QS{order.Id:D4}";
    }
}
