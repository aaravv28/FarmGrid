using Microsoft.EntityFrameworkCore;

namespace FarmGrid.Data
{
    /// <summary>
    /// FarmGrid once had a third "B2B Buyer" role. It has been removed; this
    /// deletes any remaining B2B users together with their carts, orders and
    /// Quick Sell orders, then the role itself. Safe to run on every startup.
    /// </summary>
    public static class LegacyB2BCleanup
    {
        private const string LegacyRoleName = "B2B Buyer";

        public static async Task RunAsync(ApplicationDbContext context)
        {
            var role = await context.Roles
                .FirstOrDefaultAsync(r => r.Name == LegacyRoleName);

            if (role == null)
            {
                return;
            }

            var userIds = await context.UserRoles
                .Where(ur => ur.RoleId == role.Id)
                .Select(ur => ur.UserId)
                .ToListAsync();

            await using var transaction = await context.Database.BeginTransactionAsync();

            await context.QuickSellOrders.Where(o => userIds.Contains(o.CustomerId)).ExecuteDeleteAsync();
            await context.CartItems.Where(c => userIds.Contains(c.CustomerId)).ExecuteDeleteAsync();
            await context.OrderItems.Where(oi => userIds.Contains(oi.Order!.CustomerId)).ExecuteDeleteAsync();
            await context.Orders.Where(o => userIds.Contains(o.CustomerId)).ExecuteDeleteAsync();
            await context.UserRoles.Where(ur => ur.RoleId == role.Id).ExecuteDeleteAsync();
            await context.UserClaims.Where(c => userIds.Contains(c.UserId)).ExecuteDeleteAsync();
            await context.UserLogins.Where(l => userIds.Contains(l.UserId)).ExecuteDeleteAsync();
            await context.UserTokens.Where(t => userIds.Contains(t.UserId)).ExecuteDeleteAsync();
            await context.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync();
            await context.RoleClaims.Where(rc => rc.RoleId == role.Id).ExecuteDeleteAsync();
            await context.Roles.Where(r => r.Id == role.Id).ExecuteDeleteAsync();

            await transaction.CommitAsync();
        }
    }
}
