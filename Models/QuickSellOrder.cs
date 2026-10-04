using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    public class QuickSellOrder
    {
        public int Id { get; set; }

        public int QuickSellListingId { get; set; }

        public QuickSellListing? QuickSellListing { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? CustomerEmail { get; set; }

        [Required]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [Range(0.01, 1000000)]
        [Precision(18, 2)]
        public decimal QuantityPurchased { get; set; }

        [Required]
        [Precision(18, 2)]
        public decimal PricePerKg { get; set; }

        [Required]
        [Precision(18, 2)]
        public decimal TotalAmount { get; set; }

        [StringLength(50)]
        public string PaymentMethod { get; set; } = "Cash on Delivery";

        [StringLength(50)]
        public string Status { get; set; } = "Confirmed";

        public DateTime PurchasedAt { get; set; } = DateTime.Now;
    }
}
