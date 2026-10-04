using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    /// <summary>The produce categories used by the catalog and Quick Sell alike.</summary>
    public static class ProduceCategories
    {
        public const string Vegetables = "Vegetables";
        public const string Fruits = "Fruits";
        public const string DairyProducts = "Dairy Products";
        public const string Groceries = "Groceries";

        public static readonly string[] All = [Vegetables, Fruits, DairyProducts, Groceries];
    }

    /// <summary>A unit produce is sold in. Whole-number units cannot be sold in fractions.</summary>
    public sealed record ProduceUnit(string Code, string Label, bool WholeNumbersOnly)
    {
        /// <summary>The quantity step for number inputs in this unit.</summary>
        public string Step => WholeNumbersOnly ? "1" : "0.01";
    }

    public static class ProduceUnits
    {
        public static readonly ProduceUnit Kilogram = new("kg", "kg", WholeNumbersOnly: false);
        public static readonly ProduceUnit Litre = new("L", "litre (L)", WholeNumbersOnly: false);
        public static readonly ProduceUnit Dozen = new("dozen", "dozen", WholeNumbersOnly: true);
        public static readonly ProduceUnit Bunch = new("bunch", "bunch", WholeNumbersOnly: true);
        public static readonly ProduceUnit Piece = new("piece", "piece", WholeNumbersOnly: true);

        public static readonly ProduceUnit[] All = [Kilogram, Litre, Dozen, Bunch, Piece];

        public static ProduceUnit? Find(string? code) => All.FirstOrDefault(u => u.Code == code);

        /// <summary>True if the quantity is allowed in this unit (whole numbers for dozen/piece).</summary>
        public static bool AllowsQuantity(string? code, decimal quantity) =>
            Find(code) is not { WholeNumbersOnly: true } || quantity == decimal.Truncate(quantity);
    }

    /// <summary>Fixed marketplace rules, in one place.</summary>
    public static class MarketRules
    {
        public const int QuickSellDurationHours = 48;
        public const decimal DeliveryChargePerOrder = 30m;
        public const decimal LowStockThreshold = 20m;
        public const decimal QuickSellMinimumKg = 1m;
        public const decimal DefaultQuickSellQuantityKg = 50m;

        public static readonly int[] QuickSellPresetsKg = [25, 50, 100];
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ProduceCategoryAttribute : ValidationAttribute
    {
        public ProduceCategoryAttribute() : base("Please choose one of the listed categories.") { }

        public override bool IsValid(object? value) =>
            value is null || value is string s && ProduceCategories.All.Contains(s);
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ProduceUnitAttribute : ValidationAttribute
    {
        public ProduceUnitAttribute() : base("Please choose one of the listed units.") { }

        public override bool IsValid(object? value) =>
            value is null || value is string s && ProduceUnits.Find(s) != null;
    }
}
