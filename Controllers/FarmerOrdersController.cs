using FarmGrid.Data;
using FarmGrid.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Controllers
{
    /// <summary>
    /// The owning Farmer moves Orders and Quick Sell orders from Placed to
    /// Delivered or Cancelled. Customers cannot change an order's status.
    /// </summary>
    [Authorize(Roles = Roles.Farmer)]
    public class FarmerOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FarmerOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Deliver(int id) =>
            ChangeOrder(id, order => order.MarkDelivered(), "delivered");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Cancel(int id) =>
            ChangeOrder(id, order => order.Cancel(), "cancelled and its stock returned");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> DeliverQuickSell(int id) =>
            ChangeQuickSellOrder(id, order => order.MarkDelivered(), "delivered");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> CancelQuickSell(int id) =>
            ChangeQuickSellOrder(id, order => order.Cancel(), "cancelled and its quantity returned to the lot");

        private async Task<IActionResult> ChangeOrder(int id, Action<Order> change, string outcome)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.FarmerId == farmerId);

            if (order == null)
            {
                return NotFound();
            }

            return await Apply(() => change(order), $"Order #FG{order.Id:D4} {outcome}.");
        }

        private async Task<IActionResult> ChangeQuickSellOrder(int id, Action<QuickSellOrder> change, string outcome)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var order = await _context.QuickSellOrders
                .Include(o => o.QuickSellListing)
                .FirstOrDefaultAsync(o =>
                    o.Id == id &&
                    o.QuickSellListing != null &&
                    o.QuickSellListing.FarmerId == farmerId);

            if (order == null)
            {
                return NotFound();
            }

            return await Apply(() => change(order), $"Quick Sell order #QS-{order.Id} {outcome}.");
        }

        private async Task<IActionResult> Apply(Action change, string successMessage)
        {
            try
            {
                change();
                await _context.SaveChangesAsync();
                TempData["Success"] = successMessage;
            }
            catch (InvalidOrderStatusChangeException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("FarmerDashboard", "UI");
        }
    }
}
