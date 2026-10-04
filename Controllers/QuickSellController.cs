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
        private readonly TimeProvider _time;

        public QuickSellController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            TimeProvider time)
        {
            _context = context;
            _userManager = userManager;
            _time = time;
        }

        private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        /// <summary>A lot's location defaults to the farmer's own Location.</summary>
        private static string DefaultLocation(ApplicationUser user) =>
            string.IsNullOrWhiteSpace(user.City) ? string.Empty : $"{user.City}, {user.District} district";

        // MARKETPLACE - GET
        public async Task<IActionResult> Index(string? category, string? search)
        {
            var now = UtcNow;
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

            return View("~/Views/Ui/QuickSell.cshtml", listings);
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
                Quantity = Math.Min(listing.AvailableQuantity, MarketRules.DefaultQuickSellQuantityKg)
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
            return View("~/Views/Ui/QuickSellDetails.cshtml", listing);
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
                model.Location = DefaultLocation(user);
            }

            return View("~/Views/Ui/QuickSellCreate.cshtml", model);
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
                return View("~/Views/Ui/QuickSellCreate.cshtml", model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var farmerId = user.Id;
            var farmerName = user.FullName;

            var listing = new QuickSellListing
            {
                FarmerId = farmerId,
                FarmerName = farmerName,
                Location = string.IsNullOrWhiteSpace(model.Location) ? DefaultLocation(user) : model.Location,
                CropTitle = model.CropTitle,
                Category = model.Category,
                UnitMeasure = "kg",
                BulkQuantity = model.BulkQuantity,
                AvailableQuantity = model.BulkQuantity,
                StartingPrice = model.StartingPrice,
                FloorPrice = model.FloorPrice,
                DurationHours = MarketRules.QuickSellDurationHours,
                CreatedAt = UtcNow,
                ExpiresAt = UtcNow.AddHours(MarketRules.QuickSellDurationHours),
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

            var now = UtcNow;

            if (!listing.IsBuyable(now))
            {
                TempData["Error"] = "This quick sell listing has sold out or ended.";
                return RedirectToAction(nameof(Details), new { id = model.ListingId });
            }

            if (model.Quantity <= 0)
            {
                ModelState.AddModelError(nameof(model.Quantity), "Please enter a quantity to buy.");
            }
            else if (model.Quantity < MarketRules.QuickSellMinimumKg && model.Quantity != listing.AvailableQuantity)
            {
                // Under the minimum is allowed only to clear the last of a lot
                ModelState.AddModelError(nameof(model.Quantity), $"The minimum order is {MarketRules.QuickSellMinimumKg} kg, unless you are buying all that is left.");
            }
            else if (model.Quantity > listing.AvailableQuantity)
            {
                ModelState.AddModelError(nameof(model.Quantity), $"Only {listing.AvailableQuantity} kg available.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.PurchaseModel = model;
                return View("~/Views/Ui/QuickSellDetails.cshtml", listing);
            }

            // Calculate live decay price securely on the server
            var currentPrice = listing.CalculateCurrentPrice(now);

            // The price only falls, so it should never exceed what the customer was shown
            if (currentPrice > model.ShownPricePerKg)
            {
                TempData["Error"] = $"The price is now ₹{currentPrice:F2}/kg, higher than the ₹{model.ShownPricePerKg:F2}/kg you were shown. Please review and confirm again.";
                return RedirectToAction(nameof(Details), new { id = listing.Id });
            }

            var totalAmount = Math.Round(model.Quantity * currentPrice, 2, MidpointRounding.AwayFromZero);

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var customerId = user.Id;

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
                Status = OrderStatuses.Placed,
                PurchasedAt = now
            };

            // Saved only if AvailableQuantity is unchanged since it was read (concurrency token)
            listing.AvailableQuantity -= model.Quantity;

            _context.QuickSellOrders.Add(order);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "Someone else just bought from this lot. Please check the quantity left and try again.";
                return RedirectToAction(nameof(Details), new { id = listing.Id });
            }

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

            var now = UtcNow;
            var currentPrice = listing.CalculateCurrentPrice(now);
            var remaining = listing.GetRemainingTime(now);

            return Json(new
            {
                id = listing.Id,
                currentPrice = currentPrice,
                startingPrice = listing.StartingPrice,
                floorPrice = listing.FloorPrice,
                availableQuantity = listing.AvailableQuantity,
                remainingSeconds = Math.Max(0, (int)remaining.TotalSeconds),
                isExpired = listing.IsExpired(now),
                isBuyable = listing.IsBuyable(now),
                isSoldOut = listing.IsSoldOut,
                elapsedHours = (decimal)(now - listing.CreatedAt).TotalHours
            });
        }
    }
}
