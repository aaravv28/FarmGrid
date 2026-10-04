using System.ComponentModel.DataAnnotations;

namespace FarmGrid.Models
{
    /// <summary>A Gujarat city or town and the district it belongs to.</summary>
    public sealed record GujaratCity(string Name, string District);

    /// <summary>
    /// The Locations users can choose: Gujarat's 34 districts (Vav-Tharad was carved out
    /// of Banaskantha in October 2025) and their main cities and towns. City names are
    /// unique, so a city always determines its district. Two towns share a name with a
    /// town elsewhere (Kalol, Mandvi); each is listed once, under its better-known district.
    /// </summary>
    public static class GujaratLocations
    {
        public static readonly GujaratCity[] Cities =
        [
            new("Ahmedabad", "Ahmedabad"), new("Dholka", "Ahmedabad"), new("Sanand", "Ahmedabad"), new("Viramgam", "Ahmedabad"),
            new("Amreli", "Amreli"), new("Savarkundla", "Amreli"), new("Rajula", "Amreli"),
            new("Anand", "Anand"), new("Khambhat", "Anand"), new("Petlad", "Anand"), new("Borsad", "Anand"),
            new("Modasa", "Aravalli"), new("Bayad", "Aravalli"),
            new("Palanpur", "Banaskantha"), new("Deesa", "Banaskantha"),
            new("Bharuch", "Bharuch"), new("Ankleshwar", "Bharuch"), new("Jambusar", "Bharuch"),
            new("Bhavnagar", "Bhavnagar"), new("Mahuva", "Bhavnagar"), new("Palitana", "Bhavnagar"),
            new("Botad", "Botad"), new("Gadhada", "Botad"),
            new("Chhota Udaipur", "Chhota Udaipur"), new("Bodeli", "Chhota Udaipur"),
            new("Dahod", "Dahod"), new("Jhalod", "Dahod"),
            new("Ahwa", "Dang"),
            new("Khambhalia", "Devbhumi Dwarka"), new("Dwarka", "Devbhumi Dwarka"),
            new("Gandhinagar", "Gandhinagar"), new("Kalol", "Gandhinagar"), new("Mansa", "Gandhinagar"),
            new("Veraval", "Gir Somnath"), new("Una", "Gir Somnath"), new("Kodinar", "Gir Somnath"),
            new("Jamnagar", "Jamnagar"), new("Dhrol", "Jamnagar"), new("Kalavad", "Jamnagar"),
            new("Junagadh", "Junagadh"), new("Keshod", "Junagadh"), new("Manavadar", "Junagadh"),
            new("Nadiad", "Kheda"), new("Kheda", "Kheda"), new("Kapadvanj", "Kheda"),
            new("Bhuj", "Kutch"), new("Gandhidham", "Kutch"), new("Anjar", "Kutch"), new("Mandvi", "Kutch"),
            new("Lunawada", "Mahisagar"), new("Balasinor", "Mahisagar"),
            new("Mehsana", "Mehsana"), new("Unjha", "Mehsana"), new("Visnagar", "Mehsana"), new("Kadi", "Mehsana"),
            new("Morbi", "Morbi"), new("Wankaner", "Morbi"),
            new("Rajpipla", "Narmada"), new("Dediapada", "Narmada"),
            new("Navsari", "Navsari"), new("Bilimora", "Navsari"), new("Gandevi", "Navsari"),
            new("Godhra", "Panchmahal"), new("Halol", "Panchmahal"), new("Shehera", "Panchmahal"),
            new("Patan", "Patan"), new("Sidhpur", "Patan"), new("Radhanpur", "Patan"),
            new("Porbandar", "Porbandar"), new("Ranavav", "Porbandar"),
            new("Rajkot", "Rajkot"), new("Gondal", "Rajkot"), new("Jetpur", "Rajkot"), new("Dhoraji", "Rajkot"), new("Upleta", "Rajkot"),
            new("Himmatnagar", "Sabarkantha"), new("Idar", "Sabarkantha"), new("Prantij", "Sabarkantha"),
            new("Surat", "Surat"), new("Bardoli", "Surat"), new("Olpad", "Surat"),
            new("Surendranagar", "Surendranagar"), new("Dhrangadhra", "Surendranagar"), new("Limbdi", "Surendranagar"),
            new("Vyara", "Tapi"), new("Songadh", "Tapi"),
            new("Vadodara", "Vadodara"), new("Dabhoi", "Vadodara"), new("Padra", "Vadodara"), new("Karjan", "Vadodara"),
            new("Valsad", "Valsad"), new("Vapi", "Valsad"), new("Pardi", "Valsad"),
            new("Tharad", "Vav-Tharad"), new("Vav", "Vav-Tharad"), new("Bhabhar", "Vav-Tharad"),
        ];

        public static readonly string[] Districts =
            Cities.Select(c => c.District).Distinct().Order().ToArray();

        /// <summary>The district of a listed city, or null if the city is not listed.</summary>
        public static string? DistrictOf(string? city) =>
            Cities.FirstOrDefault(c => c.Name == city)?.District;

        public static IEnumerable<GujaratCity> CitiesIn(string? district) =>
            Cities.Where(c => c.District == district);
    }

    /// <summary>The value must be one of the listed Gujarat cities.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class GujaratCityAttribute : ValidationAttribute
    {
        public GujaratCityAttribute() : base("Please choose your city from the list.") { }

        public override bool IsValid(object? value) =>
            value is null || value is string s && GujaratLocations.DistrictOf(s) != null;
    }
}
