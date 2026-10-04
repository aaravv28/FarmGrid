using FarmGrid.Models;
using System.ComponentModel.DataAnnotations;

namespace FarmGrid.ViewModels
{
    /// <summary>
    /// The fields a host Farmer sets when scheduling a Trip. Owner, state and
    /// participants are set by the server, never bound.
    /// </summary>
    public class TripInputModel
    {
        [Required]
        [MarketName]
        public string DestinationMarket { get; set; } = string.Empty;

        [Required]
        public DateTime DispatchDate { get; set; }

        [Required]
        public string VehicleType { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalVehicleCost { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal HostCargoWeightKg { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal AvailableCapacityKg { get; set; }
    }
}
