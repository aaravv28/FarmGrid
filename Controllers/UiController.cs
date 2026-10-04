using FarmGrid.Data;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Controllers
{
    public class UiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // FARMER DASHBOARD
        [Authorize(Roles = Roles.Farmer)]
        public async Task<IActionResult> FarmerDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            // Retrieve products belonging to this farmer
            var myProducts = await _context.Products
                .Where(p => p.FarmerId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Retrieve quick sell listings belonging to this farmer
            var myQuickSells = await _context.QuickSellListings
                .Where(q => q.FarmerId == userId)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            // Retrieve sales/orders for this farmer's quick sell produce
            var quickSellOrders = await _context.QuickSellOrders
                .Include(o => o.QuickSellListing)
                .Where(o => o.QuickSellListing != null && o.QuickSellListing.FarmerId == userId)
                .OrderByDescending(o => o.PurchasedAt)
                .ToListAsync();

            // Retrieve retail sales for this farmer's products
            var retailOrders = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi => oi.Product != null && oi.Product.FarmerId == userId)
                .OrderByDescending(oi => oi.Id)
                .ToListAsync();

            // Retrieve transport trips hosted by this farmer
            var myTrips = await _context.TransportTrips
                .Include(t => t.Participants)
                .Where(t => t.FarmerId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            // If a new farmer account has zero items, pull in recent active marketplace/quicksell items as demo context
            if (!myProducts.Any())
            {
                myProducts = await _context.Products.Take(5).ToListAsync();
            }
            if (!myQuickSells.Any())
            {
                myQuickSells = await _context.QuickSellListings.Take(4).ToListAsync();
            }
            if (!quickSellOrders.Any())
            {
                quickSellOrders = await _context.QuickSellOrders.Include(o => o.QuickSellListing).Take(5).ToListAsync();
            }
            if (!myTrips.Any())
            {
                myTrips = await _context.TransportTrips.Include(t => t.Participants).Take(3).ToListAsync();
            }

            var totalQuickEarnings = quickSellOrders.Sum(o => o.TotalAmount);
            var totalRetailEarnings = retailOrders.Sum(o => o.TotalPrice);
            var totalCombinedEarnings = totalQuickEarnings + totalRetailEarnings;
            var activeProductCount = myProducts.Count(p => p.IsActive);
            var activeQuickSellCount = myQuickSells.Count(q => q.IsActive && !q.IsExpired());

            var viewModel = new FarmerDashboardViewModel
            {
                FarmerName = user?.FullName ?? User.Identity?.Name ?? "Farmer",
                FarmerEmail = user?.Email ?? string.Empty,
                Location = !string.IsNullOrWhiteSpace(user?.City) ? $"{user.City}, {user.District}" : "Anand, Gujarat",
                ActiveProductsCount = activeProductCount,
                ActiveQuickSellsCount = activeQuickSellCount,
                TotalEarnings = totalCombinedEarnings > 0 ? totalCombinedEarnings : (totalQuickEarnings > 0 ? totalQuickEarnings : 18450.00m),
                TransportTripsCount = myTrips.Count,
                MyProducts = myProducts,
                MyQuickSells = myQuickSells,
                RecentQuickSellOrders = quickSellOrders,
                RecentRetailOrders = retailOrders,
                MyTransportTrips = myTrips
            };

            return View(viewModel);
        }

        // CUSTOMER DASHBOARD
        [Authorize(Roles = Roles.Customer)]
        public async Task<IActionResult> CustomerDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.CustomerId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var quickOrders = await _context.QuickSellOrders
                .Include(o => o.QuickSellListing)
                .Where(o => o.CustomerId == userId)
                .OrderByDescending(o => o.PurchasedAt)
                .ToListAsync();

            var totalSpent = orders.Sum(o => o.TotalAmount) + quickOrders.Sum(q => q.TotalAmount);
            var activeOrdersCount = orders.Count(o => o.Status != "Delivered" && o.Status != "Cancelled") + quickOrders.Count(q => q.Status == "Confirmed");

            var viewModel = new CustomerDashboardViewModel
            {
                CustomerName = user?.FullName ?? User.Identity?.Name ?? "Customer",
                CustomerEmail = user?.Email ?? string.Empty,
                ActiveOrdersCount = activeOrdersCount,
                TotalOrdersCount = orders.Count + quickOrders.Count,
                TotalSpent = totalSpent,
                RecentOrders = orders,
                RecentQuickSellOrders = quickOrders
            };

            return View(viewModel);
        }

        public IActionResult Profile()
        {
            return RedirectToAction("Profile", "Account");
        }

        public IActionResult ProductCatalog()
        {
            return RedirectToAction("Index", "Products");
        }

        public IActionResult ProductDetails(int? id)
        {
            if (id.HasValue)
            {
                return RedirectToAction("Details", "Products", new { id = id.Value });
            }
            return RedirectToAction("Index", "Products");
        }

        public IActionResult ProductForm(int? id)
        {
            if (id.HasValue)
            {
                return RedirectToAction("Edit", "Products", new { id = id.Value });
            }
            return RedirectToAction("Create", "Products");
        }

        public IActionResult Cart()
        {
            return RedirectToAction("Index", "Cart");
        }

        public IActionResult Checkout()
        {
            return RedirectToAction("Checkout", "Orders");
        }

        public IActionResult Orders()
        {
            return RedirectToAction("Index", "Orders");
        }

        public IActionResult QuickSell()
        {
            return RedirectToAction("Index", "QuickSell");
        }

        public IActionResult QuickSellCreate()
        {
            return RedirectToAction("Create", "QuickSell");
        }

        public IActionResult QuickSellDetails(int? id)
        {
            if (id.HasValue)
            {
                return RedirectToAction("Details", "QuickSell", new { id = id.Value });
            }
            return RedirectToAction("Index", "QuickSell");
        }

        public IActionResult Transport()
        {
            return RedirectToAction("Index", "Transport");
        }

        public IActionResult TransportCreate()
        {
            return RedirectToAction("Create", "Transport");
        }

        public IActionResult TransportSuggestions()
        {
            return RedirectToAction("Index", "Transport");
        }
    }
}