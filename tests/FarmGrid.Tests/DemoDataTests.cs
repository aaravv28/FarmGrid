using FarmGrid.Data;
using FarmGrid.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace FarmGrid.Tests
{
    public class DemoDataTests
    {
        private static readonly DateTimeOffset FirstRun = new(2026, 10, 1, 6, 0, 0, TimeSpan.Zero);

        private static async Task TopUp(TestDb db, DateTimeOffset at, bool isDevelopment)
        {
            await using var context = db.CreateContext();
            await DemoData.TopUpAsync(context, "farmer", "Ramesh Patel", new FakeTimeProvider(at), isDevelopment);
        }

        private static async Task<(int BuyableLots, int OpenTrips, int AllLots, int AllTrips)> Counts(TestDb db, DateTimeOffset at)
        {
            await using var context = db.CreateContext();
            var now = at.UtcDateTime;
            var today = IndiaTime.Today(new FakeTimeProvider(at));
            return (
                await context.QuickSellListings.Buyable(now).CountAsync(),
                await context.TransportTrips.Open(today).CountAsync(),
                await context.QuickSellListings.CountAsync(),
                await context.TransportTrips.CountAsync());
        }

        [Fact]
        public async Task An_empty_database_gets_live_demo_lots_and_upcoming_trips_with_hosts()
        {
            using var db = new TestDb();
            await TopUp(db, FirstRun, isDevelopment: false);

            var counts = await Counts(db, FirstRun);
            Assert.True(counts.BuyableLots > 0);
            Assert.True(counts.OpenTrips > 0);

            await using var context = db.CreateContext();
            var trips = await context.TransportTrips.Include(t => t.Participants).ToListAsync();
            Assert.All(trips, t => Assert.Single(t.Participants, p => p.IsHost && p.FarmerId == "farmer" && p.FareShare == t.TotalVehicleCost));
            Assert.All(await context.QuickSellListings.ToListAsync(), l => Assert.Equal("Ramesh Patel", l.FarmerName));
        }

        [Fact]
        public async Task In_development_a_demo_that_has_gone_stale_is_topped_up_and_history_kept()
        {
            using var db = new TestDb();
            await TopUp(db, FirstRun, isDevelopment: true);
            var initial = await Counts(db, FirstRun);

            var aWeekLater = FirstRun.AddDays(7);
            var stale = await Counts(db, aWeekLater);
            Assert.Equal(0, stale.BuyableLots);
            Assert.Equal(0, stale.OpenTrips);

            await TopUp(db, aWeekLater, isDevelopment: true);

            var after = await Counts(db, aWeekLater);
            Assert.True(after.BuyableLots > 0);
            Assert.True(after.OpenTrips > 0);
            Assert.Equal(initial.AllLots * 2, after.AllLots);
            Assert.Equal(initial.AllTrips * 2, after.AllTrips);
        }

        [Fact]
        public async Task A_live_demo_is_left_alone()
        {
            using var db = new TestDb();
            await TopUp(db, FirstRun, isDevelopment: true);
            var initial = await Counts(db, FirstRun);

            await TopUp(db, FirstRun.AddHours(1), isDevelopment: true);

            Assert.Equal(initial.AllLots, (await Counts(db, FirstRun)).AllLots);
            Assert.Equal(initial.AllTrips, (await Counts(db, FirstRun)).AllTrips);
        }

        [Fact]
        public async Task Outside_development_existing_data_is_never_topped_up()
        {
            using var db = new TestDb();
            await TopUp(db, FirstRun, isDevelopment: false);
            var initial = await Counts(db, FirstRun);

            await TopUp(db, FirstRun.AddDays(7), isDevelopment: false);

            Assert.Equal(initial.AllLots, (await Counts(db, FirstRun)).AllLots);
            Assert.Equal(initial.AllTrips, (await Counts(db, FirstRun)).AllTrips);
        }
    }
}
