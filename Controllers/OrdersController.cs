using FarmGrid.Data;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
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

            LoadCheckoutSummary(cartItems);

            return View(
                "~/Views/UI/Checkout.cshtml",
                new CheckoutViewModel());
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

            LoadCheckoutSummary(cartItems);

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
                decimal subtotal = 0;

                foreach (var cart in cartItems)
                {
                    if (cart.Product == null ||
                        !cart.Product.IsActive)
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

                    subtotal +=
                        cart.Product.UnitPrice *
                        cart.Quantity;
                }

                const decimal deliveryCharge = 30;

                var order = new Order
                {
                    CustomerId = customerId,
                    CustomerName = model.CustomerName,
                    PhoneNumber = model.PhoneNumber,
                    DeliveryAddress = model.DeliveryAddress,
                    City = model.City,
                    DeliverySlot = model.DeliverySlot,
                    PaymentMethod = model.PaymentMethod,
                    Status = "Placed",
                    Subtotal = subtotal,
                    DeliveryCharge = deliveryCharge,
                    TotalAmount =
                        subtotal + deliveryCharge,
                    CreatedAt = DateTime.Now
                };

                _context.Orders.Add(order);

                await _context.SaveChangesAsync();

                foreach (var cart in cartItems)
                {
                    var product = cart.Product!;

                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = product.Id,
                        ProductTitle = product.Title,
                        UnitMeasure = product.UnitMeasure,
                        Quantity = cart.Quantity,
                        UnitPrice = product.UnitPrice,
                        TotalPrice =
                            product.UnitPrice *
                            cart.Quantity
                    };

                    _context.OrderItems.Add(orderItem);

                    product.StockQuantity -=
                        cart.Quantity;

                    if (product.StockQuantity <= 0)
                    {
                        product.StockQuantity = 0;
                        product.IsActive = false;
                    }
                }

                _context.CartItems.RemoveRange(
                    cartItems);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return RedirectToAction(
                    nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "",
                    ex.Message);

                LoadCheckoutSummary(cartItems);

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
                "~/Views/UI/Orders.cshtml",
                new List<Order> { order });
        }

        private void LoadCheckoutSummary(
            IEnumerable<CartItem> cartItems)
        {
            decimal subtotal = cartItems
                .Where(c => c.Product != null)
                .Sum(c =>
                    c.Product!.UnitPrice *
                    c.Quantity);

            const decimal deliveryCharge = 30;

            ViewBag.Subtotal = subtotal;
            ViewBag.DeliveryCharge = deliveryCharge;
            ViewBag.Total =
                subtotal + deliveryCharge;
        }
    }
}