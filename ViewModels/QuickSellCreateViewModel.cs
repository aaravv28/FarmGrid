using System.ComponentModel.DataAnnotations;

namespace FarmGrid.ViewModels
{
    public class QuickSellCreateViewModel
    {
        [Required(ErrorMessage = "Crop title is required")]
        [StringLength(120)]
        [Display(Name = "Crop / Produce Name")]
        public string CropTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        [StringLength(50)]
        public string Category { get; set; } = "Vegetables";

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, 1000000, ErrorMessage = "Quantity must be at least 1 kg")]
        [Display(Name = "Lot Quantity (kg)")]
        public decimal BulkQuantity { get; set; }

        [Required(ErrorMessage = "Starting price is required")]
        [Range(0.01, 100000, ErrorMessage = "Starting price must be greater than 0")]
        [Display(Name = "Starting Price (₹/kg)")]
        public decimal StartingPrice { get; set; }

        [Required(ErrorMessage = "Minimum floor price is required")]
        [Range(0.01, 100000, ErrorMessage = "Floor price must be greater than 0")]
        [Display(Name = "Minimum Floor Price (₹/kg)")]
        public decimal FloorPrice { get; set; }

        [Display(Name = "Location (City/District)")]
        [StringLength(150)]
        public string? Location { get; set; }

        [StringLength(500)]
        [Display(Name = "Harvest Notes / Description")]
        public string? Description { get; set; }
    }
}
