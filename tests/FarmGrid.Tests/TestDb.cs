using FarmGrid.Data;
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
        private readonly SqliteConnection _connection;

        public TestDb()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var context = CreateContext();
            context.Database.EnsureCreated();
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
