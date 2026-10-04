using FarmGrid.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FarmGrid.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }

        public DbSet<CartItem> CartItems { get; set; }

        public DbSet<Order> Orders { get; set; }

        public DbSet<OrderItem> OrderItems { get; set; }

        public DbSet<TransportTrip> TransportTrips { get; set; }

        public DbSet<TransportParticipant> TransportParticipants { get; set; }

        public DbSet<QuickSellListing> QuickSellListings { get; set; }

        public DbSet<QuickSellOrder> QuickSellOrders { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<QuickSellOrder>()
                .HasOne(qso => qso.QuickSellListing)
                .WithMany(qsl => qsl.Orders)
                .HasForeignKey(qso => qso.QuickSellListingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CartItem>()
                .HasOne(c => c.Product)
                .WithMany(p => p.CartItems)
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TransportParticipant>()
                .HasOne(tp => tp.TransportTrip)
                .WithMany(t => t.Participants)
                .HasForeignKey(tp => tp.TransportTripId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CartItem>()
                .HasIndex(c => new
                {
                    c.CustomerId,
                    c.ProductId
                })
                .IsUnique();

            builder.Entity<TransportParticipant>()
                .HasIndex(tp => new
                {
                    tp.TransportTripId,
                    tp.FarmerId
                })
                .IsUnique();

            // Owners are always real users (no placeholder ids). Restrict: a user
            // who owns products, lots or trips cannot be deleted out from under them.
            builder.Entity<Product>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.FarmerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<QuickSellListing>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(q => q.FarmerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TransportTrip>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.FarmerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TransportParticipant>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(tp => tp.FarmerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Order>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(o => o.FarmerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Optimistic concurrency: a save only succeeds if these values are still
            // what the request read, so two requests racing for the same stock,
            // capacity or order cannot both win. The losing save throws
            // DbUpdateConcurrencyException, which controllers report to the user.
            builder.Entity<Product>()
                .Property(p => p.StockQuantity)
                .IsConcurrencyToken();

            builder.Entity<QuickSellListing>()
                .Property(q => q.AvailableQuantity)
                .IsConcurrencyToken();

            builder.Entity<TransportTrip>()
                .Property(t => t.AvailableCapacityKg)
                .IsConcurrencyToken();

            builder.Entity<Order>()
                .Property(o => o.Status)
                .IsConcurrencyToken();

            builder.Entity<QuickSellOrder>()
                .Property(o => o.Status)
                .IsConcurrencyToken();
        }
    }
}