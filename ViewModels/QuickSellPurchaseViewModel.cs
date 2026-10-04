using System.ComponentModel.DataAnnotations;

namespace FarmGrid.ViewModels
{
    public class QuickSellPurchaseViewModel
    {
        [Required]
        public int ListingId { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, 1000000, ErrorMessage = "Quantity must be at least 1 kg")]
        [Display(Name = "Purchase Quantity (kg)")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Your Name")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Phone]
        [Display(Name = "Contact Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Delivery address is required")]
        [Display(Name = "Delivery Address / Warehouse Address")]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required")]
        [Display(Name = "City / District")]
        public string City { get; set; } = string.Empty;

        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Cash on Delivery / Direct Settlement";
    }
}
