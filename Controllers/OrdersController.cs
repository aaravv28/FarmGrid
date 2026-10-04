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
    [Authorize(Roles = Roles.Customer)]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrdersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var customerId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(
                "~/Views/UI/Orders.cshtml",
                orders);
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var customerId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.CustomerId == customerId)
                .ToListAsync();

            if (!cartItems.Any())
            {
                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            await LoadCheckoutSummaryAsync(cartItems);

            var user = await _userManager.GetUserAsync(User);
            var model = new CheckoutViewModel();
            if (user != null)
            {
                model.CustomerName = user.FullName;
                model.PhoneNumber = user.PhoneNumber ?? string.Empty;
                model.City = user.City ?? string.Empty;
                model.DeliveryAddress = !string.IsNullOrWhiteSpace(user.District) ? $"{user.City}, {user.District}" : (user.City ?? string.Empty);
            }

            return View(
                "~/Views/UI/Checkout.cshtml",
                model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(
            CheckoutViewModel model)
        {
            var customerId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (customerId == null)
            {
                return Challenge();
            }

            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.CustomerId == customerId)
                .ToListAsync();

            await LoadCheckoutSummaryAsync(cartItems);

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/UI/Checkout.cshtml",
                    model);
            }

            if (!cartItems.Any())
            {
                ModelState.AddModelError(
                    "",
                    "Your cart is empty.");

                return View(
                    "~/Views/UI/Checkout.cshtml",
                    model);
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                foreach (var cart in cartItems)
                {
                    if (cart.Product == null ||
                        !cart.Product.IsAvailable)
                    {
                        throw new Exception(
                            "A product is no longer available.");
                    }

                    if (cart.Quantity >
                        cart.Product.StockQuantity)
                    {
                        throw new Exception(
                            $"Insufficient stock for {cart.Product.Title}.");
                    }
                }

                // One Order per Farmer: each delivers, and charges delivery, separately
                foreach (var farmerPlan in CheckoutPlan.Build(cartItems))
                {
                    var order = new Order
                    {
                        CustomerId = customerId,
                        FarmerId = farmerPlan.FarmerId,
                        CustomerName = model.CustomerName,
                        PhoneNumber = model.PhoneNumber,
                        DeliveryAddress = model.DeliveryAddress,
                        City = model.City,
                        DeliverySlot = model.DeliverySlot,
                        PaymentMethod = PaymentMethods.CashOnDelivery,
                        Status = OrderStatuses.Placed,
                        Subtotal = farmerPlan.Subtotal,
                        DeliveryCharge = farmerPlan.DeliveryCharge,
                        TotalAmount = farmerPlan.Total,
                        CreatedAt = DateTime.Now
                    };

                    foreach (var cart in farmerPlan.Items)
                    {
                        var product = cart.Product!;

                        order.OrderItems.Add(new OrderItem
                        {
                            ProductId = product.Id,
                            ProductTitle = product.Title,
                            UnitMeasure = product.UnitMeasure,
                            Quantity = cart.Quantity,
                            UnitPrice = product.UnitPrice,
                            TotalPrice =
                                product.UnitPrice *
                                cart.Quantity
                        });

                        product.StockQuantity -=
                            cart.Quantity;
                    }

                    _context.Orders.Add(order);
                }

                _context.CartItems.RemoveRange(
                    cartItems);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return RedirectToAction(
                    nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "",
                    "Stock for an item in your cart changed while you were checking out. Please review your cart and try again.");

                await LoadCheckoutSummaryAsync(cartItems);

                return View(
                    "~/Views/UI/Checkout.cshtml",
                    model);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "",
                    ex.Message);

                await LoadCheckoutSummaryAsync(cartItems);

                return View(
                    "~/Views/UI/Checkout.cshtml",
                    model);
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var customerId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o =>
                    o.Id == id &&
                    o.CustomerId == customerId);

            if (order == null)
            {
                return NotFound();
            }

            return View(
                "~/Views/UI/OrderDetails.cshtml",
                order);
        }

        private async Task LoadCheckoutSummaryAsync(
            IEnumerable<CartItem> cartItems)
        {
            var plan = CheckoutPlan.Build(cartItems);

            var farmerIds = plan
                .Where(p => p.FarmerId != null)
                .Select(p => p.FarmerId!)
                .ToList();

            ViewBag.Plan = plan;
            ViewBag.FarmerNames = await _context.Users
                .Where(u => farmerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName);
            ViewBag.Subtotal = plan.Sum(p => p.Subtotal);
            ViewBag.DeliveryCharge = plan.Sum(p => p.DeliveryCharge);
            ViewBag.Total = plan.Sum(p => p.Total);
        }
    }
}