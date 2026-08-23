using FarmGrid.Data;
using FarmGrid.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var customerId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var cartItems =
                await _context.CartItems
                    .Include(c => c.Product)
                    .Where(c =>
                        c.CustomerId ==
                        customerId)
                    .ToListAsync();

            return View(
                "~/Views/UI/Cart.cshtml",
                cartItems);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(
            int productId,
            decimal quantity = 1)
        {
            var customerId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (customerId == null)
            {
                return Challenge();
            }

            var product =
                await _context.Products
                    .FirstOrDefaultAsync(p =>
                        p.Id == productId &&
                        p.IsActive);

            if (product == null)
            {
                return NotFound();
            }

            if (quantity <= 0 ||
                quantity > product.StockQuantity)
            {
                TempData["Error"] =
                    "Invalid quantity.";

                return RedirectToAction(
                    "Details",
                    "Products",
                    new { id = productId });
            }

            var existing =
                await _context.CartItems
                    .FirstOrDefaultAsync(c =>
                        c.CustomerId ==
                        customerId &&
                        c.ProductId ==
                        productId);

            if (existing != null)
            {
                var newQuantity =
                    existing.Quantity +
                    quantity;

                if (newQuantity >
                    product.StockQuantity)
                {
                    TempData["Error"] =
                        "Quantity exceeds available stock.";

                    return RedirectToAction(
                        nameof(Index));
                }

                existing.Quantity =
                    newQuantity;
            }
            else
            {
                _context.CartItems.Add(
                    new CartItem
                    {
                        CustomerId =
                            customerId,

                        ProductId =
                            productId,

                        Quantity =
                            quantity,

                        AddedAt =
                            DateTime.Now
                    });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            int id,
            decimal quantity)
        {
            var customerId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var item =
                await _context.CartItems
                    .Include(c => c.Product)
                    .FirstOrDefaultAsync(c =>
                        c.Id == id &&
                        c.CustomerId ==
                        customerId);

            if (item == null)
            {
                return NotFound();
            }

            if (quantity <= 0)
            {
                _context.CartItems.Remove(item);
            }
            else
            {
                if (item.Product == null ||
                    quantity >
                    item.Product.StockQuantity)
                {
                    TempData["Error"] =
                        "Quantity exceeds available stock.";

                    return RedirectToAction(
                        nameof(Index));
                }

                item.Quantity = quantity;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(
            int id)
        {
            var customerId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var item =
                await _context.CartItems
                    .FirstOrDefaultAsync(c =>
                        c.Id == id &&
                        c.CustomerId ==
                        customerId);

            if (item == null)
            {
                return NotFound();
            }

            _context.CartItems.Remove(item);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}