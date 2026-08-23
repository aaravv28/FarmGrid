using Microsoft.EntityFrameworkCore;

namespace FarmGrid.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public Order? Order { get; set; }

        public int ProductId { get; set; }

        public Product? Product { get; set; }

        public string ProductTitle { get; set; } = string.Empty;

        public string UnitMeasure { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal Quantity { get; set; }

        [Precision(18, 2)]
        public decimal UnitPrice { get; set; }

        [Precision(18, 2)]
        public decimal TotalPrice { get; set; }
    }
}
