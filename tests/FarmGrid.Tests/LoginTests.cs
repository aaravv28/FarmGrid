using FarmGrid.Controllers;
using FarmGrid.Data;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FarmGrid.Tests
{
    public class LoginTests
    {
        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }

        private static (AccountController Controller, UserManager<ApplicationUser> Users) AccountFor(ApplicationDbContext context)
        {
            var options = Options.Create(new IdentityOptions());
            AccountController.ConfigureLockout(options.Value.Lockout);

            var users = new UserManager<ApplicationUser>(
                new UserStore<ApplicationUser>(context), options, new PasswordHasher<ApplicationUser>(),
                [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
                NullLogger<UserManager<ApplicationUser>>.Instance);

            var httpContext = new DefaultHttpContext();
            var signIn = new SignInManager<ApplicationUser>(
                users,
                new HttpContextAccessor { HttpContext = httpContext },
                new UserClaimsPrincipalFactory<ApplicationUser>(users, options),
                options,
                NullLogger<SignInManager<ApplicationUser>>.Instance,
                null!,
                new DefaultUserConfirmation<ApplicationUser>());

            var controller = new AccountController(users, signIn, null!, TimeProvider.System)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
            };
            return (controller, users);
        }

        private static string LoginError(AccountController controller) =>
            Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage;

        [Fact]
        public async Task Unknown_email_and_wrong_password_get_the_same_message()
        {
            using var db = new TestDb();
            await using var context = db.CreateContext();
            var (_, users) = AccountFor(context);
            await users.CreateAsync(new ApplicationUser { UserName = "priya@x.com", Email = "priya@x.com", FullName = "Priya" }, "Secret@123");

            var (unknown, _) = AccountFor(context);
            await unknown.Login(new LoginViewModel { Email = "nobody@x.com", Password = "Secret@123" });

            var (wrongPassword, _) = AccountFor(context);
            await wrongPassword.Login(new LoginViewModel { Email = "priya@x.com", Password = "Wrong@123" });

            Assert.Equal(LoginError(unknown), LoginError(wrongPassword));
            Assert.DoesNotContain("No user", LoginError(unknown));
        }

        [Fact]
        public async Task Repeated_wrong_passwords_lock_the_account()
        {
            using var db = new TestDb();
            await using var context = db.CreateContext();
            var (controller, users) = AccountFor(context);
            var user = new ApplicationUser { UserName = "priya@x.com", Email = "priya@x.com", FullName = "Priya" };
            await users.CreateAsync(user, "Secret@123");

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var (attemptController, _) = AccountFor(context);
                await attemptController.Login(new LoginViewModel { Email = "priya@x.com", Password = "Wrong@123" });
            }

            Assert.True(await users.IsLockedOutAsync(user));

            var (afterLockout, _) = AccountFor(context);
            await afterLockout.Login(new LoginViewModel { Email = "priya@x.com", Password = "Secret@123" });
            Assert.Contains("locked", LoginError(afterLockout), StringComparison.OrdinalIgnoreCase);
        }
    }
}
