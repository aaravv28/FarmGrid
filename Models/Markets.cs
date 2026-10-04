using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    /// <summary>A real agricultural market (APMC yard) a Trip can go to.</summary>
    public sealed record Market(string Name, string City, string District);

    /// <summary>
    /// The markets Trips can go to: main Gujarat APMC yards. Trips store the market's
    /// Name; add a market here and it appears in every form and search.
    /// </summary>
    public static class Markets
    {
        public static readonly Market Ahmedabad = new("Ahmedabad APMC", "Ahmedabad", "Ahmedabad");
        public static readonly Market Anand = new("Anand APMC", "Anand", "Anand");
        public static readonly Market Nadiad = new("Nadiad APMC", "Nadiad", "Kheda");
        public static readonly Market Vadodara = new("Vadodara APMC", "Vadodara", "Vadodara");
        public static readonly Market Bharuch = new("Bharuch APMC", "Bharuch", "Bharuch");
        public static readonly Market Surat = new("Surat APMC", "Surat", "Surat");
        public static readonly Market Mehsana = new("Mehsana APMC", "Mehsana", "Mehsana");
        public static readonly Market Unjha = new("Unjha APMC", "Unjha", "Mehsana");
        public static readonly Market Rajkot = new("Rajkot APMC", "Rajkot", "Rajkot");
        public static readonly Market Gondal = new("Gondal APMC", "Gondal", "Rajkot");
        public static readonly Market Junagadh = new("Junagadh APMC", "Junagadh", "Junagadh");
        public static readonly Market Amreli = new("Amreli APMC", "Amreli", "Amreli");
        public static readonly Market Bhavnagar = new("Bhavnagar APMC", "Bhavnagar", "Bhavnagar");

        public static readonly Market[] All =
        [
            Ahmedabad, Anand, Nadiad, Vadodara, Bharuch, Surat, Mehsana, Unjha,
            Rajkot, Gondal, Junagadh, Amreli, Bhavnagar
        ];

        public static Market? Find(string? name) => All.FirstOrDefault(m => m.Name == name);
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MarketNameAttribute : ValidationAttribute
    {
        public MarketNameAttribute() : base("Please choose one of the listed markets.") { }

        public override bool IsValid(object? value) =>
            value is null || value is string s && Markets.Find(s) != null;
    }
}
