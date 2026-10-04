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
    public class QuickSellController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public QuickSellController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // MARKETPLACE - GET
        public async Task<IActionResult> Index(string? category, string? search)
        {
            // Auto-seed initial demo quick-sells if none exist
            await EnsureSeedDataAsync();

            var now = DateTime.Now;
            var query = _context.QuickSellListings
                .Buyable(now);

            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                query = query.Where(q => q.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(q => q.CropTitle.Contains(search) || q.Location.Contains(search));
            }

            var listings = await query
                .OrderBy(q => q.ExpiresAt)
                .ToListAsync();

            ViewBag.SelectedCategory = category ?? "All";
            ViewBag.Search = search ?? string.Empty;

            return View("~/Views/UI/QuickSell.cshtml", listings);
        }

        // DETAILS - GET
        public async Task<IActionResult> Details(int id)
        {
            var listing = await _context.QuickSellListings
                .FirstOrDefaultAsync(q => q.Id == id);

            if (listing == null)
            {
                return NotFound("Quick sell listing not found.");
            }

            var purchaseModel = new QuickSellPurchaseViewModel
            {
                ListingId = listing.Id,
                Quantity = Math.Min(listing.AvailableQuantity, 50)
            };

            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    purchaseModel.CustomerName = user.FullName;
                    purchaseModel.PhoneNumber = user.PhoneNumber ?? string.Empty;
                    purchaseModel.City = user.City ?? string.Empty;
                    purchaseModel.DeliveryAddress = user.District != null ? $"{user.City}, {user.District}" : (user.City ?? string.Empty);
                }
            }

            ViewBag.PurchaseModel = purchaseModel;
            return View("~/Views/UI/QuickSellDetails.cshtml", listing);
        }

        // CREATE - GET
        [Authorize(Roles = Roles.Farmer)]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new QuickSellCreateViewModel();
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                model.Location = !string.IsNullOrWhiteSpace(user.City)
                    ? $"{user.City}{(string.IsNullOrWhiteSpace(user.District) ? "" : $", {user.District}")}"
                    : "Anand, Gujarat";
            }

            return View("~/Views/UI/QuickSellCreate.cshtml", model);
        }

        // CREATE - POST
        [Authorize(Roles = Roles.Farmer)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuickSellCreateViewModel model)
        {
            if (model.FloorPrice > model.StartingPrice)
            {
                ModelState.AddModelError(nameof(model.FloorPrice), "Floor price cannot be higher than starting price.");
            }

            if (!ModelState.IsValid)
            {
                return View("~/Views/UI/QuickSellCreate.cshtml", model);
            }

            var user = await _userManager.GetUserAsync(User);
            var farmerId = user?.Id ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous-farmer";
            var farmerName = user?.FullName ?? user?.UserName ?? "Farmer";

            var listing = new QuickSellListing
            {
                FarmerId = farmerId,
                FarmerName = farmerName,
                Location = model.Location ?? (!string.IsNullOrWhiteSpace(user?.City) ? $"{user.City}, {user.District}" : "Anand, Gujarat"),
                CropTitle = model.CropTitle,
                Category = model.Category,
                UnitMeasure = "kg",
                BulkQuantity = model.BulkQuantity,
                AvailableQuantity = model.BulkQuantity,
                StartingPrice = model.StartingPrice,
                FloorPrice = model.FloorPrice,
                DurationHours = 48,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddHours(48),
                IsActive = true,
                Description = model.Description
            };

            _context.QuickSellListings.Add(listing);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Quick Sell listing '{listing.CropTitle}' created! Automatic 48-hour price decay is now active.";
            return RedirectToAction(nameof(Details), new { id = listing.Id });
        }

        // PURCHASE - POST
        [Authorize(Roles = Roles.Customer)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Purchase(QuickSellPurchaseViewModel model)
        {
            var listing = await _context.QuickSellListings
                .FirstOrDefaultAsync(q => q.Id == model.ListingId);

            if (listing == null)
            {
                return NotFound("Listing not found.");
            }

            if (!listing.IsBuyable())
            {
                TempData["Error"] = "This quick sell listing has sold out or ended.";
                return RedirectToAction(nameof(Details), new { id = model.ListingId });
            }

            if (model.Quantity <= 0)
            {
                ModelState.AddModelError(nameof(model.Quantity), "Quantity must be at least 1 kg.");
            }
            else if (model.Quantity > listing.AvailableQuantity)
            {
                ModelState.AddModelError(nameof(model.Quantity), $"Only {listing.AvailableQuantity} kg available.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.PurchaseModel = model;
                return View("~/Views/UI/QuickSellDetails.cshtml", listing);
            }

            // Calculate live decay price securely on the server
            var currentPrice = listing.CalculateCurrentPrice();
            var totalAmount = Math.Round(model.Quantity * currentPrice, 2);

            var user = await _userManager.GetUserAsync(User);
            var customerId = user?.Id ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "guest-customer";

            var order = new QuickSellOrder
            {
                QuickSellListingId = listing.Id,
                CustomerId = customerId,
                CustomerName = model.CustomerName,
                CustomerEmail = user?.Email,
                PhoneNumber = model.PhoneNumber,
                DeliveryAddress = model.DeliveryAddress,
                City = model.City,
                QuantityPurchased = model.Quantity,
                PricePerKg = currentPrice,
                TotalAmount = totalAmount,
                PaymentMethod = PaymentMethods.CashOnDelivery,
                Status = "Confirmed",
                PurchasedAt = DateTime.Now
            };

            // Atomically decrement available stock
            listing.AvailableQuantity -= model.Quantity;

            _context.QuickSellOrders.Add(order);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Order placed successfully! Purchased {model.Quantity} kg of {listing.CropTitle} at ₹{currentPrice:F2}/kg (Total: ₹{totalAmount:F2}).";
            return RedirectToAction(nameof(Details), new { id = listing.Id });
        }

        // LIVE PRICE API ENDPOINT FOR DYNAMIC FRONTEND CLIENT TICK
        [HttpGet]
        [Route("api/quicksell/price/{id}")]
        public async Task<IActionResult> GetLivePrice(int id)
        {
            var listing = await _context.QuickSellListings
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == id);

            if (listing == null)
            {
                return NotFound();
            }

            var currentPrice = listing.CalculateCurrentPrice();
            var remaining = listing.GetRemainingTime();

            return Json(new
            {
                id = listing.Id,
                currentPrice = currentPrice,
                startingPrice = listing.StartingPrice,
                floorPrice = listing.FloorPrice,
                availableQuantity = listing.AvailableQuantity,
                remainingSeconds = Math.Max(0, (int)remaining.TotalSeconds),
                isExpired = listing.IsExpired(),
                isSoldOut = listing.IsSoldOut,
                elapsedHours = (decimal)(DateTime.Now - listing.CreatedAt).TotalHours
            });
        }

        // DEMO SEED HELPER
        private async Task EnsureSeedDataAsync()
        {
            if (await _context.QuickSellListings.AnyAsync())
            {
                return;
            }

            var now = DateTime.Now;
            var sampleListings = new List<QuickSellListing>
            {
                new QuickSellListing
                {
                    FarmerId = "seed-farmer-1",
                    FarmerName = "Ramesh Patel",
                    Location = "Anand, Gujarat",
                    CropTitle = "Fresh Tomatoes",
                    Category = "Vegetables",
                    UnitMeasure = "kg",
                    BulkQuantity = 500,
                    AvailableQuantity = 350,
                    StartingPrice = 30.00m,
                    FloorPrice = 15.00m,
                    DurationHours = 48,
                    CreatedAt = now.AddHours(-18),
                    ExpiresAt = now.AddHours(30),
                    IsActive = true,
                    Description = "High-grade organic ripe hybrid tomatoes, harvested this morning. Needs fast clearing."
                },
                new QuickSellListing
                {
                    FarmerId = "seed-farmer-2",
                    FarmerName = "Kishore Bhai",
                    Location = "Kheda, Gujarat",
                    CropTitle = "Fresh Carrots",
                    Category = "Vegetables",
                    UnitMeasure = "kg",
                    BulkQuantity = 800,
                    AvailableQuantity = 620,
                    StartingPrice = 45.00m,
                    FloorPrice = 25.00m,
                    DurationHours = 48,
                    CreatedAt = now.AddHours(-12),
                    ExpiresAt = now.AddHours(36),
                    IsActive = true,
                    Description = "Fresh crunchy orange carrots directly sorted from field. Ideal for processing or home use."
                },
                new QuickSellListing
                {
                    FarmerId = "seed-farmer-3",
                    FarmerName = "Dinesh Somani",
                    Location = "Vadodara, Gujarat",
                    CropTitle = "Alphonso & Kesar Mangoes",
                    Category = "Fruits",
                    UnitMeasure = "kg",
                    BulkQuantity = 400,
                    AvailableQuantity = 400,
                    StartingPrice = 120.00m,
                    FloorPrice = 75.00m,
                    DurationHours = 48,
                    CreatedAt = now.AddHours(-6),
                    ExpiresAt = now.AddHours(42),
                    IsActive = true,
                    Description = "Naturally ripened sweet mango crates. Perfect commercial grade sweetness."
                }
            };

            _context.QuickSellListings.AddRange(sampleListings);
            await _context.SaveChangesAsync();
        }
    }
}
