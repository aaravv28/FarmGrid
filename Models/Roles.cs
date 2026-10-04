namespace FarmGrid.Models
{
    /// <summary>
    /// The two FarmGrid roles. Identity roles are the single source of truth
    /// for which role a user has.
    /// </summary>
    public static class Roles
    {
        public const string Farmer = "Farmer";
        public const string Customer = "Customer";

        public static readonly string[] All = [Farmer, Customer];
    }
}
