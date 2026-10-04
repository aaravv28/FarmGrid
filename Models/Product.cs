using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    public class Product
    {
        public int Id { get; set; }

        /// <summary>The Farmer who owns this Product; always an existing user.</summary>
        [Required]
        public string FarmerId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public string UnitMeasure { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue)]
        [Precision(18, 2)]
        public decimal UnitPrice { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        [Precision(18, 2)]
        public decimal StockQuantity { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>False only when the farmer has deleted the product. Selling out never changes it.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Customers can see and buy a product while it is listed and in stock.</summary>
        public bool IsAvailable => IsActive && StockQuantity > 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<CartItem> CartItems { get; set; }
            = new List<CartItem>();

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}