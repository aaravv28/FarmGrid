using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    public class TransportTrip
    {
        public int Id { get; set; }

        [Required]
        public string FarmerId { get; set; } = string.Empty;

        [Required]
        public string DestinationMarket { get; set; } = string.Empty;

        [Required]
        public DateTime DispatchDate { get; set; }

        [Required]
        public string VehicleType { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue)]
        [Precision(18, 2)]
        public decimal TotalVehicleCost { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        [Precision(18, 2)]
        public decimal HostCargoWeightKg { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        [Precision(18, 2)]
        public decimal AvailableCapacityKg { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<TransportParticipant> Participants { get; set; }
            = new List<TransportParticipant>();
    }
}