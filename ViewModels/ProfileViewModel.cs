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

        [Display(Name = "City")]
        public string? City { get; set; }

        [Display(Name = "District")]
        public string? District { get; set; }

        [Display(Name = "Role")]
        public string UserRole { get; set; } = string.Empty;
    }
}
