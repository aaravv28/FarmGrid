using FarmGrid.Models;

namespace FarmGrid.ViewModels
{
    public class CustomerDashboardViewModel
    {
        public string CustomerName { get; set; } = "Customer";
        public string CustomerEmail { get; set; } = string.Empty;

        public int ActiveOrdersCount { get; set; }
        public int TotalOrdersCount { get; set; }
        public decimal TotalSpent { get; set; }

        public List<Order> RecentOrders { get; set; } = new();
        public List<QuickSellOrder> RecentQuickSellOrders { get; set; } = new();
    }
}
