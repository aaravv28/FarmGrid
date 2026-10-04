using FarmGrid.Models;
using Microsoft.EntityFrameworkCore;

namespace FarmGrid.Data
{
    /// <summary>
    /// Demo Quick Sell lots and shared trips owned by the demo farmer. Lots expire after
    /// 48 hours and trips close after their dispatch date, so in Development a fresh set
    /// is added whenever none is live; existing rows (and any orders on them) are kept.
    /// Outside Development, demo data is only added to an empty database.
    /// </summary>
    public static class DemoData
    {
        public static async Task TopUpAsync(
            ApplicationDbContext context,
            string farmerId,
            string farmerName,
            TimeProvider time,
            bool isDevelopment)
        {
            var now = time.GetUtcNow().UtcDateTime;
            var today = IndiaTime.Today(time);

            var needsLots = isDevelopment
                ? !await context.QuickSellListings.Buyable(now).AnyAsync()
                : !await context.QuickSellListings.AnyAsync();

            if (needsLots)
            {
                context.QuickSellListings.AddRange(QuickSellLots(farmerId, farmerName, now));
            }

            var needsTrips = isDevelopment
                ? !await context.TransportTrips.Open(today).AnyAsync()
                : !await context.TransportTrips.AnyAsync();

            if (needsTrips)
            {
                context.TransportTrips.AddRange(Trips(farmerId, now, today));
            }

            await context.SaveChangesAsync();
        }

        private static IEnumerable<QuickSellListing> QuickSellLots(string farmerId, string farmerName, DateTime now)
        {
            QuickSellListing Lot(string location, string crop, string category, decimal bulk, decimal available,
                decimal start, decimal floor, int hoursAgo, string description) => new()
            {
                FarmerId = farmerId,
                FarmerName = farmerName,
                Location = location,
                CropTitle = crop,
                Category = category,
                UnitMeasure = "kg",
                BulkQuantity = bulk,
                AvailableQuantity = available,
                StartingPrice = start,
                FloorPrice = floor,
                DurationHours = 48,
                CreatedAt = now.AddHours(-hoursAgo),
                ExpiresAt = now.AddHours(48 - hoursAgo),
                IsActive = true,
                Description = description
            };

            yield return Lot("Anand, Gujarat", "Fresh Tomatoes", "Vegetables", 500, 350, 30.00m, 15.00m, 18,
                "High-grade organic ripe hybrid tomatoes, harvested this morning. Needs fast clearing.");
            yield return Lot("Kheda, Gujarat", "Orange Carrots", "Vegetables", 800, 620, 45.00m, 25.00m, 12,
                "Fresh crunchy orange carrots directly sorted from field. Ideal for processing or home use.");
            yield return Lot("Vadodara, Gujarat", "Alphonso & Kesar Mangoes", "Fruits", 400, 400, 120.00m, 75.00m, 6,
                "Naturally ripened sweet mango crates. Perfect commercial grade sweetness.");
        }

        private static IEnumerable<TransportTrip> Trips(string farmerId, DateTime now, DateTime today)
        {
            TransportTrip Trip(string market, int daysAhead, string vehicle, decimal cost, decimal hostCargo, decimal spare) => new()
            {
                FarmerId = farmerId,
                DestinationMarket = market,
                DispatchDate = today.AddDays(daysAhead),
                VehicleType = vehicle,
                TotalVehicleCost = cost,
                HostCargoWeightKg = hostCargo,
                AvailableCapacityKg = spare,
                IsActive = true,
                CreatedAt = now,
                Participants =
                {
                    new TransportParticipant
                    {
                        FarmerId = farmerId,
                        CargoWeightKg = hostCargo,
                        FareShare = cost,
                        IsHost = true,
                        JoinedAt = now
                    }
                }
            };

            yield return Trip("Central Mandi", 1, "Tata Ace 1.5 Ton", 2800.00m, 600.00m, 900.00m);
            yield return Trip("City Hub", 2, "Mahindra Bolero Maxi", 4200.00m, 1200.00m, 1300.00m);
            yield return Trip("Wholesale Market", 3, "Eicher Pro 2049", 6500.00m, 2500.00m, 2000.00m);
        }
    }
}
