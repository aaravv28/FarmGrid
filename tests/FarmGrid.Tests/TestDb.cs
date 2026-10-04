using FarmGrid.Data;
using FarmGrid.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FarmGrid.Tests
{
    /// <summary>
    /// An isolated in-memory SQLite database per test. SQLite (unlike the EF
    /// InMemory provider) enforces foreign keys and supports transactions,
    /// so controller logic that relies on them behaves as it does in production.
    /// </summary>
    public sealed class TestDb : IDisposable
    {
        /// <summary>
        /// Farmer users every test database starts with, so products, lots and trips
        /// can reference a real owner (owner ids are foreign keys to users).
        /// </summary>
        public static readonly string[] SeededFarmerIds = ["farmer", "farmer-a", "farmer-b", "host", "joiner", "other"];

        private readonly SqliteConnection _connection;

        public TestDb()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var context = CreateContext();
            context.Database.EnsureCreated();

            context.Users.AddRange(SeededFarmerIds.Select(id => new ApplicationUser
            {
                Id = id,
                UserName = $"{id}@farmgrid.test",
                FullName = id
            }));
            context.SaveChanges();
        }

        public ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new ApplicationDbContext(options);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
