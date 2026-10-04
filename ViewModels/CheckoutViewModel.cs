using System.ComponentModel.DataAnnotations;

namespace FarmGrid.ViewModels
{
    public class CheckoutViewModel
    {
        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(50)]
        public string? DeliverySlot { get; set; }
    }
}
