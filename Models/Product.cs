using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    public class Product
    {
        public int Id { get; set; }

        public string? FarmerId { get; set; }

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

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<CartItem> CartItems { get; set; }
            = new List<CartItem>();

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}