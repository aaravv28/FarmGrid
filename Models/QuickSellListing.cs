using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    public class QuickSellListing
    {
        public int Id { get; set; }

        [Required]
        public string FarmerId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FarmerName { get; set; } = string.Empty;

        [StringLength(150)]
        public string Location { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string CropTitle { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = "Vegetables";

        [Required]
        [StringLength(20)]
        public string UnitMeasure { get; set; } = "kg";

        [Required]
        [Range(1, 1000000)]
        [Precision(18, 2)]
        public decimal BulkQuantity { get; set; }

        [Required]
        [Range(0, 1000000)]
        [Precision(18, 2)]
        public decimal AvailableQuantity { get; set; }

        [Required]
        [Range(0.01, 100000)]
        [Precision(18, 2)]
        public decimal StartingPrice { get; set; }

        [Required]
        [Range(0.01, 100000)]
        [Precision(18, 2)]
        public decimal FloorPrice { get; set; }

        public int DurationHours { get; set; } = 48;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddHours(48);

        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        public string? Description { get; set; }

        public ICollection<QuickSellOrder> Orders { get; set; } = new List<QuickSellOrder>();

        /// <summary>
        /// 48-Hour Price Decay Formula:
        /// CurrentPrice(t) = max(FloorPrice, StartPrice - ((StartPrice - FloorPrice) / DurationHours) * ElapsedHours)
        /// </summary>
        public decimal CalculateCurrentPrice(DateTime? asOf = null)
        {
            var now = asOf ?? DateTime.Now;
            var elapsedHours = (decimal)(now - CreatedAt).TotalHours;

            if (elapsedHours <= 0)
            {
                return StartingPrice;
            }

            if (elapsedHours >= DurationHours || now >= ExpiresAt)
            {
                return FloorPrice;
            }

            var hourlyDrop = (StartingPrice - FloorPrice) / DurationHours;
            var current = StartingPrice - (hourlyDrop * elapsedHours);

            if (current < FloorPrice)
            {
                return FloorPrice;
            }

            return Math.Round(current, 2);
        }

        public TimeSpan GetRemainingTime(DateTime? asOf = null)
        {
            var now = asOf ?? DateTime.Now;
            var remaining = ExpiresAt - now;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        public bool IsExpired(DateTime? asOf = null)
        {
            var now = asOf ?? DateTime.Now;
            return now >= ExpiresAt || AvailableQuantity <= 0;
        }
    }
}
