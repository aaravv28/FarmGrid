using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [Required]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required]
        public string City { get; set; } = string.Empty;

        public string? DeliverySlot { get; set; }

        public string PaymentMethod { get; set; }
            = "Cash on Delivery";

        public string Status { get; set; } = "Placed";

        [Precision(18, 2)]
        public decimal Subtotal { get; set; }

        [Precision(18, 2)]
        public decimal DeliveryCharge { get; set; }

        [Precision(18, 2)]
        public decimal TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}
