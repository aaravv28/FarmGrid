using FarmGrid.Data;
using FarmGrid.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? search,
            string? category)
        {
            var query = _context.Products
                .Where(p => p.IsActive);

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

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == id &&
                    p.IsActive);

            if (product == null)
            {
                return NotFound();
            }

            return View(
                "~/Views/UI/ProductDetails.cshtml",
                product);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(
                "~/Views/UI/ProductForm.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Product product)
        {
            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/UI/ProductForm.cshtml",
                    product);
            }

            product.FarmerId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            product.CreatedAt = DateTime.Now;
            product.IsActive = true;

            _context.Products.Add(product);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product =
                await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            return View(
                "~/Views/UI/ProductForm.cshtml",
                product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Product model)
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

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/UI/ProductForm.cshtml",
                    model);
            }

            product.Title = model.Title;
            product.Category = model.Category;
            product.UnitMeasure = model.UnitMeasure;
            product.UnitPrice = model.UnitPrice;
            product.StockQuantity =
                model.StockQuantity;
            product.Description =
                model.Description;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

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

            product.IsActive = false;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}