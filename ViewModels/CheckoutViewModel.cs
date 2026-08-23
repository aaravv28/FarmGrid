using System.ComponentModel.DataAnnotations;

namespace FarmGrid.ViewModels
{
    public class CheckoutViewModel
    {
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
    }
}
