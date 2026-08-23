using System.ComponentModel.DataAnnotations;

namespace FarmGrid.ViewModels
{
    public class TransportJoinViewModel
    {
        public int TripId { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal CargoWeightKg { get; set; }
    }
}
