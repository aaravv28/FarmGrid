using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;

        /// <summary>The Farmer whose products this Order contains, and who fulfils it.</summary>
        public string? FarmerId { get; set; }

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
            = PaymentMethods.CashOnDelivery;

        public string Status { get; set; } = OrderStatuses.Placed;

        [Precision(18, 2)]
        public decimal Subtotal { get; set; }

        [Precision(18, 2)]
        public decimal DeliveryCharge { get; set; }

        [Precision(18, 2)]
        public decimal TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();

        public void MarkDelivered()
        {
            OrderStatuses.EnsurePlaced(Status);
            Status = OrderStatuses.Delivered;
        }

        /// <summary>Cancels the order and returns each item's quantity to its Product's stock. Requires OrderItems with Product loaded.</summary>
        public void Cancel()
        {
            OrderStatuses.EnsurePlaced(Status);

            foreach (var item in OrderItems)
            {
                var product = item.Product
                    ?? throw new InvalidOperationException("Order items must be loaded with their Product to cancel.");
                product.StockQuantity += item.Quantity;
            }

            Status = OrderStatuses.Cancelled;
        }
    }
}
