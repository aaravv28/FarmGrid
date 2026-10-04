using FarmGrid.Data;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // PRODUCT CATALOG - Open to everyone
        public async Task<IActionResult> Index(
            string? search,
            string? category)
        {
            var query = _context.Products
                .Available();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    p.Title.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(p =>
                    p.Category == category);
            }

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(
                "~/Views/UI/ProductCatalog.cshtml",
                products);
        }

        // PRODUCT DETAILS - Open to everyone
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Available()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(
                "~/Views/UI/ProductDetails.cshtml",
                product);
        }

        // CREATE - GET (Strictly Farmer)
        [Authorize(Roles = Roles.Farmer)]
        [HttpGet]
        public IActionResult Create()
        {
            return View(
                "~/Views/UI/ProductForm.cshtml",
                new ProductInputModel());
        }

        // CREATE - POST (Strictly Farmer)
        [Authorize(Roles = Roles.Farmer)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            ProductInputModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/UI/ProductForm.cshtml",
                    model);
            }

            var product = new Product
            {
                FarmerId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                CreatedAt = DateTime.Now,
                IsActive = true
            };
            model.ApplyTo(product);

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Product '{product.Title}' listed successfully!";
            return RedirectToAction(nameof(Index));
        }

        // EDIT - GET (Strictly Farmer)
        [Authorize(Roles = Roles.Farmer)]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product =
                await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(product.FarmerId) && product.FarmerId != currentUserId)
            {
                TempData["Error"] = "You can only edit your own listed products.";
                return RedirectToAction(nameof(Index));
            }

            return View(
                "~/Views/UI/ProductForm.cshtml",
                ProductInputModel.From(product));
        }

        // EDIT - POST (Strictly Farmer)
        [Authorize(Roles = Roles.Farmer)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            ProductInputModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var product =
                await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(product.FarmerId) && product.FarmerId != currentUserId)
            {
                TempData["Error"] = "You can only modify your own products.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/UI/ProductForm.cshtml",
                    model);
            }

            model.ApplyTo(product);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "This product's stock changed while you were editing (an order was placed or cancelled). Please review it and save again.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            TempData["Success"] = $"Product '{product.Title}' updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // DELETE (Strictly Farmer)
        [Authorize(Roles = Roles.Farmer)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product =
                await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(product.FarmerId) && product.FarmerId != currentUserId)
            {
                TempData["Error"] = "You can only remove your own products.";
                return RedirectToAction(nameof(Index));
            }

            product.IsActive = false;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "This product's stock changed at the same moment. Please try removing it again.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Product '{product.Title}' removed from catalog.";
            return RedirectToAction(nameof(Index));
        }
    }
}