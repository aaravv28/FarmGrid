using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FarmGrid.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly TimeProvider _time;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            TimeProvider time)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _time = time;
        }

        private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        /// <summary>
        /// Lockout policy: five wrong passwords lock an account for 15 minutes.
        /// Applied in Program.cs; kept here beside the login it protects.
        /// </summary>
        public static void ConfigureLockout(LockoutOptions lockout)
        {
            lockout.AllowedForNewUsers = true;
            lockout.MaxFailedAccessAttempts = 5;
            lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }

        // REGISTER - GET
        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new RegisterViewModel());
        }

        // REGISTER - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Ensure valid role
            if (!Roles.All.Contains(model.Role))
            {
                model.Role = Roles.Customer;
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "An account with this email address already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                City = model.City,
                District = GujaratLocations.DistrictOf(model.City),
                CreatedAt = UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // Ensure role exists in Identity
                if (!await _roleManager.RoleExistsAsync(model.Role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(model.Role));
                }

                await _userManager.AddToRoleAsync(user, model.Role);
                await _signInManager.SignInAsync(user, isPersistent: false);

                TempData["Success"] = $"Welcome to FarmGrid, {user.FullName}!";

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                // Redirect based on selected role
                if (model.Role == Roles.Farmer)
                {
                    return RedirectToAction("FarmerDashboard", "UI");
                }

                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // LOGIN - GET
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // LOGIN - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // The same message for an unknown email and a wrong password, so the
            // login form cannot be used to discover which emails have accounts
            const string invalidLogin = "Invalid email or password.";

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, invalidLogin);
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                TempData["Success"] = $"Welcome back, {user.FullName}!";

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                if (await _userManager.IsInRoleAsync(user, Roles.Farmer))
                {
                    return RedirectToAction("FarmerDashboard", "UI");
                }
                else if (await _userManager.IsInRoleAsync(user, Roles.Customer))
                {
                    return RedirectToAction("CustomerDashboard", "UI");
                }

                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "This account is locked after too many failed attempts. Please try again in 15 minutes.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, invalidLogin);
            return View(model);
        }

        // LOGOUT - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            TempData["Success"] = "You have been logged out safely.";
            return RedirectToAction("Index", "Home");
        }

        // PROFILE - GET
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                City = user.City,
                District = user.District,
                Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty
            };

            return View(model);
        }

        // PROFILE - POST
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.City = model.City;
            user.District = GujaratLocations.DistrictOf(model.City);

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Your profile has been updated successfully!";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // ACCESS DENIED
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
