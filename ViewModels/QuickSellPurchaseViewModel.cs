using System.ComponentModel.DataAnnotations;

namespace FarmGrid.ViewModels
{
    public class QuickSellPurchaseViewModel
    {
        [Required]
        public int ListingId { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(0.01, 1000000, ErrorMessage = "Please enter a quantity to buy")]
        [Display(Name = "Purchase Quantity (kg)")]
        public decimal Quantity { get; set; }

        /// <summary>The price per kg the customer saw when they placed the order. They are never charged more.</summary>
        [Range(0.01, 100000, ErrorMessage = "Please reload the page to see the current price.")]
        public decimal ShownPricePerKg { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Your Name")]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Phone]
        [Display(Name = "Contact Phone Number")]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Delivery address is required")]
        [Display(Name = "Delivery Address")]
        [StringLength(200)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required")]
        [Display(Name = "City / District")]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;
    }
}
