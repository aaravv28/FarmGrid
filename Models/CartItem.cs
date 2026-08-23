using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    public class CartItem
    {
        public int Id { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;

        public int ProductId { get; set; }

        public Product? Product { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal Quantity { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}