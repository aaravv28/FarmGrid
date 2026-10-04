using FarmGrid.Models;

namespace FarmGrid.ViewModels
{
    public class FarmerDashboardViewModel
    {
        public string FarmerName { get; set; } = "Farmer";
        public string FarmerEmail { get; set; } = string.Empty;
        public string? Location { get; set; }

        public int ActiveProductsCount { get; set; }
        public int ActiveQuickSellsCount { get; set; }
        public decimal TotalEarnings { get; set; }
        public int TransportTripsCount { get; set; }

        public List<Product> MyProducts { get; set; } = new();
        public List<QuickSellListing> MyQuickSells { get; set; } = new();
        public List<QuickSellOrder> RecentQuickSellOrders { get; set; } = new();
        public List<Order> RecentRetailOrders { get; set; } = new();
        public List<TransportTrip> MyTransportTrips { get; set; } = new();
    }
}
