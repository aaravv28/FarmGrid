using Microsoft.AspNetCore.Mvc;

namespace FarmGrid.Controllers
{
    public class UiController : Controller
    {
        public IActionResult FarmerDashboard()
        {
            return View();
        }

        public IActionResult CustomerDashboard()
        {
            return View();
        }

        public IActionResult Profile()
        {
            return RedirectToAction("Profile", "Account");
        }

        public IActionResult ProductCatalog()
        {
            return View();
        }

        public IActionResult ProductDetails()
        {
            return View();
        }

        public IActionResult ProductForm()
        {
            return View();
        }

        public IActionResult Cart()
        {
            return View();
        }

        public IActionResult Checkout()
        {
            return View();
        }

        public IActionResult Orders()
        {
            return View();
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
            return View();
        }

        public IActionResult TransportCreate()
        {
            return View();
        }

        public IActionResult TransportSuggestions()
        {
            return View();
        }
    }
}