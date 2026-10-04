using FarmGrid.Models;
using System.ComponentModel.DataAnnotations;

namespace FarmGrid.ViewModels
{
    /// <summary>
    /// The fields a Farmer may set when creating or editing a Product. Owner,
    /// listing state and related orders are set by the server, never bound.
    /// </summary>
    public class ProductInputModel : IValidatableObject
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [ProduceCategory]
        public string Category { get; set; } = string.Empty;

        [Required]
        [ProduceUnit]
        public string UnitMeasure { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal StockQuantity { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!ProduceUnits.AllowsQuantity(UnitMeasure, StockQuantity))
            {
                yield return new ValidationResult(
                    $"Stock in {UnitMeasure} must be a whole number.",
                    [nameof(StockQuantity)]);
            }
        }

        public static ProductInputModel From(Product product) => new()
        {
            Id = product.Id,
            Title = product.Title,
            Category = product.Category,
            UnitMeasure = product.UnitMeasure,
            UnitPrice = product.UnitPrice,
            StockQuantity = product.StockQuantity,
            Description = product.Description
        };

        public void ApplyTo(Product product)
        {
            product.Title = Title;
            product.Category = Category;
            product.UnitMeasure = UnitMeasure;
            product.UnitPrice = UnitPrice;
            product.StockQuantity = StockQuantity;
            product.Description = Description;
        }
    }
}
