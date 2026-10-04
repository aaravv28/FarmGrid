using System.ComponentModel.DataAnnotations;

namespace FarmGrid.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Full Name is required.")]
        [Display(Name = "Full Name")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Contact Number")]
        [Phone]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Please choose your city.")]
        [Display(Name = "City")]
        [StringLength(100)]
        [FarmGrid.Models.GujaratCity]
        public string? City { get; set; }

        /// <summary>Shown for the chosen city; the server always sets it from the city.</summary>
        [Display(Name = "District")]
        [StringLength(100)]
        public string? District { get; set; }

        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;
    }
}
