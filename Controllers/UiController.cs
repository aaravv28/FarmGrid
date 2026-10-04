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

            // Retrieve retail orders this farmer fulfils
            var retailOrders = await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.FarmerId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            // Retrieve transport trips hosted by this farmer
            var myTrips = await _context.TransportTrips
                .Include(t => t.Participants)
                .Where(t => t.FarmerId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var totalQuickEarnings = quickSellOrders
                .Where(o => OrderStatuses.CountsTowardsTotals(o.Status))
                .Sum(o => o.TotalAmount);
            var totalRetailEarnings = retailOrders
                .Where(o => OrderStatuses.CountsTowardsTotals(o.Status))
                .Sum(o => o.Subtotal);
            var totalCombinedEarnings = totalQuickEarnings + totalRetailEarnings;
            var activeProductCount = myProducts.Count(p => p.IsActive);
            var activeQuickSellCount = myQuickSells.Count(q => q.IsBuyable());

            var viewModel = new FarmerDashboardViewModel
            {
                FarmerName = user?.FullName ?? User.Identity?.Name ?? "Farmer",
                FarmerEmail = user?.Email ?? string.Empty,
                Location = string.Join(", ", new[] { user?.City, user?.District }.Where(part => !string.IsNullOrWhiteSpace(part))),
                ActiveProductsCount = activeProductCount,
                ActiveQuickSellsCount = activeQuickSellCount,
                TotalEarnings = totalCombinedEarnings,
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

            var totalSpent =
                orders.Where(o => OrderStatuses.CountsTowardsTotals(o.Status)).Sum(o => o.TotalAmount) +
                quickOrders.Where(q => OrderStatuses.CountsTowardsTotals(q.Status)).Sum(q => q.TotalAmount);
            var activeOrdersCount =
                orders.Count(o => o.Status == OrderStatuses.Placed) +
                quickOrders.Count(q => q.Status == OrderStatuses.Placed);

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