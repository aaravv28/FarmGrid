using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? District { get; set; }

        [Required]
        [StringLength(30)]
        public string UserRole { get; set; } = "Customer"; // "Farmer", "Customer", "B2B Buyer"

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
