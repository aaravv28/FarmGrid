using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    public class TransportParticipant
    {
        public int Id { get; set; }

        public int TransportTripId { get; set; }

        public TransportTrip? TransportTrip { get; set; }

        [Required]
        public string FarmerId { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue)]
        [Precision(18, 2)]
        public decimal CargoWeightKg { get; set; }

        [Precision(18, 2)]
        public decimal FareShare { get; set; }

        public bool IsHost { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.Now;
    }
}