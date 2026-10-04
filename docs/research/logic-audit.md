# Logic & Consistency Audit (issue #2)

**Question:** What logic errors, incorrect behaviour, and inconsistencies exist in FarmGrid beyond those already decided on map issue #1?

**Scope:** `Controllers/`, `Models/`, `Data/DbInitializer.cs`, `ViewModels/`, `Views/`, `wwwroot/js/site.js`, and README-vs-code drift. Audited at commit `6cc2425` (main).

**Excluded (already ticketed, so not listed as findings):** removing B2B (#4), renaming `Buyer*` to `Customer*` (#5), stock>0 visibility and not flipping `IsActive` on sell-out (#6), removing Online payment (#7), one Order per Farmer with ₹30 each (#8), the Placed→Delivered|Cancelled lifecycle (#9), trips closing after dispatch date (#10), the xUnit project (#3). Where a finding touches one of these tickets, it says so.

**Method:** I read every file in scope and cite `file:line` against `6cc2425`. For framework behaviour I went to the ASP.NET Core source (cited where used). Nothing was run. The failure scenarios come from reading the code, and each one names a concrete input.

## Summary

| Severity | Count |
| --- | --- |
| High | 6 |
| Medium | 13 |
| Low | 15 |
| **Total** | **34** |

Severity guide. **High:** money, stock or data integrity can be wrong, or one user can see or change another user's data. **Medium:** a user-visible wrong result or a broken flow. **Low:** hardening, a latent problem, dead code, or cosmetic inconsistency.

---

## High

### H1. Over-posting: entity binding lets a farmer insert arbitrary OrderItems, CartItems or trip Participants
- **Where:** `Controllers/ProductsController.cs:80-98` (`Create(Product product)` then `_context.Products.Add(product)`). `Controllers/TransportController.cs:47-72` (`Create(TransportTrip trip)` then `_context.TransportTrips.Add(trip)`). The navigations are settable: `Models/Product.cs:39-43` (`CartItems`, `OrderItems`) and `Models/TransportTrip.cs:41-42` (`Participants`).
- **Failure scenario:** A farmer edits the Create Product form and adds the fields `OrderItems[0].OrderId=7&OrderItems[0].ProductTitle=X&OrderItems[0].Quantity=100&OrderItems[0].UnitPrice=999&OrderItems[0].TotalPrice=99900`. MVC model binding fills the collection. `Add(product)` inserts the whole graph, so customer order #7 gets a fake line item and the farmer's "Total Sales" grows by ₹99,900 (`UiController.cs:52-57,85`). The same works on Transport Create with `Participants[0].FarmerId=<victim id>&Participants[0].CargoWeightKg=5000`: the victim appears on the trip and `RecalculateFareShares` (`TransportController.cs:322-352`) gives them most of the fare. Their `AvailableCapacityKg` is never reduced. Posting `Id=5` also makes the insert fail with an IDENTITY_INSERT error (HTTP 500).
- **Proposed fix:** Bind input view models instead of entities, for example `ProductInputModel` and `TransportTripCreateModel` that contain only the fields the user may set, and map them to new entities. At minimum add `[Bind(...)]` whitelists. Add tests that post nested collection fields and assert no child rows are created.

### H2. Quick Sell purchase is a lost-update race, so it oversells
- **Where:** `Controllers/QuickSellController.cs:155-219`. The listing is read at `:157`, availability is checked against that in-memory copy at `:175`, the decrement happens in memory at `:211-216`, and the write is a single `SaveChanges` at `:219`. There is no transaction, no `[Timestamp]`/rowversion on `QuickSellListing` (`Models/QuickSellListing.cs`), and no conditional UPDATE.
- **Failure scenario:** 100 kg are available. Customers A and B each submit 80 kg at the same moment. Both requests read 100, both pass the check, and both write `AvailableQuantity = 20`. The result is 160 kg sold, 20 kg shown as remaining, and two `QuickSellOrder` rows. The `<= 0 → 0` clamp at `:212-215` hides the case where the numbers would go negative.
- **Proposed fix:** Add a `byte[] RowVersion` with `[Timestamp]` to `QuickSellListing` and catch `DbUpdateConcurrencyException`, then re-read and retry or report "stock changed". Alternatively, decrement atomically: `ExecuteUpdateAsync(s => s.SetProperty(q => q.AvailableQuantity, q => q.AvailableQuantity - qty))` with `Where(q => q.Id == id && q.AvailableQuantity >= qty)`, check that the affected row count is 1, and run it in the same transaction as the order insert. Add a concurrency test.

### H3. Checkout is a lost-update race on `Product.StockQuantity`
- **Where:** `Controllers/OrdersController.cs:90-93` loads the cart and its products before the transaction. The transaction at `:115` uses the default READ COMMITTED isolation and takes no locks. Stock is checked in memory at `:132-137` and set in memory at `:186-193`. `Product` has no rowversion (`Models/Product.cs`).
- **Failure scenario:** Stock is 10 kg and two customers each check out 6 kg. Both pass the check, and both write `StockQuantity = 4`, so 12 kg are sold against 10 kg of stock. When a third order makes the value negative, the clamp at `:189-193` silently sets it to 0. The transaction only makes each checkout atomic. It does nothing to serialize two checkouts.
- **Proposed fix:** Add a `[Timestamp] RowVersion` to `Product` and handle `DbUpdateConcurrencyException` by rolling back and showing "stock changed, please review your cart". Alternatively, use conditional decrements (`ExecuteUpdateAsync ... Where(StockQuantity >= qty)`) inside the transaction and check affected rows. This should be implemented together with #8 (per-farmer split), which rewrites the same loop.

### H4. Trip join race overbooks capacity and can create duplicate participants
- **Where:** `Controllers/TransportController.cs:193-237` checks the duplicate participant and the capacity before the transaction starts at `:239`. The capacity decrement at `:270` is in memory. `TransportTrip` has no rowversion, and `TransportParticipant` has no unique index on `(TransportTripId, FarmerId)` (`Data/ApplicationDbContext.cs:59-63`, where only the FK is configured).
- **Failure scenario:** A trip has 300 kg spare. Farmers X and Y join with 250 kg each at the same moment. Both pass `:230`, both write `AvailableCapacityKg = 50`, and the truck now carries 200 kg more than its stated capacity. A double-click by the same farmer can also insert two participant rows, because the `Any(p => p.FarmerId == farmerId)` check at `:213` runs before either insert commits. The farmer then pays two fare shares and capacity is reduced twice.
- **Proposed fix:** Add a rowversion to `TransportTrip` (or use a conditional `ExecuteUpdate` on capacity) and a unique index `HasIndex(p => new { p.TransportTripId, p.FarmerId }).IsUnique()`. Move the checks inside the transaction. Handle the concurrency and unique-violation exceptions with a friendly error.

### H5. Farmer dashboard "demo fallback" shows other farmers' data and customer PII, and invents earnings
- **Where:** `Controllers/UiController.cs:67-82` (fallback queries without any `FarmerId` filter) and `:84-97` (earnings, including the hard-coded `18450.00m` at `:97`).
- **Failure scenario:** A newly registered farmer opens the dashboard. Under "My Marketplace Products" they see the first 5 products of every farmer, with Edit buttons. They also see other farmers' Quick Sell listings and trips, and the first 5 Quick Sell orders on the platform, including customer names and cities (`FarmerDashboard.cshtml:261-279`). "Total Sales" shows either those strangers' order totals or a made-up ₹18,450. The orders fallback also triggers for a farmer who *has* listings but no orders yet, so real farmers see fake sales too.
- **Proposed fix:** Delete the four fallback blocks and the ₹18,450 literal. Rely on the existing empty states in `FarmerDashboard.cshtml` (`:102-110`, `:185-193`, `:241-246`, `:341-349`). Add a test: a farmer with no data gets zero earnings and empty lists.

### H6. The live Quick Sell ticker shows a different price and countdown from what the server charges
- **Where:** `Views/Ui/QuickSellDetails.cshtml:304-305` serializes server-local `CreatedAt`/`ExpiresAt` with no offset (`yyyy-MM-ddTHH:mm:ss`). The browser's `new Date()` reads those strings as **browser-local** time. The 30-second server sync at `:373-383` sets `currentLivePrice`, but the next one-second `updateTick` (`:350`) overwrites it with the client calculation, so the sync never has an effect. The server recomputes the price at POST time (`QuickSellController.cs:187`). Rounding also differs: C# `Math.Round` uses banker's rounding (`Models/QuickSellListing.cs:92`) and JS `Math.round` rounds half up (`:328`).
- **Failure scenario:** The server runs in UTC (any cloud host) and the customer is in IST (UTC+5:30). The browser treats `CreatedAt` as 5.5 h earlier than it really was. The ticker therefore shows the price 5.5 h further decayed and a countdown 5.5 h shorter. The customer clicks "Lock Price & Place Order (₹X)" and is charged the server's *higher* price. The same mismatch appears whenever the client clock is wrong. README:38 claims the sync "prevents race conditions", which it does not.
- **Proposed fix:** Emit UTC ISO-8601 with an offset (`ExpiresAt.ToUniversalTime().ToString("o")`), together with the DateTime UTC migration (M10). Drive the display from the server: use `remainingSeconds` and `currentPrice` from `/api/quicksell/price/{id}` (`QuickSellController.cs:228-252`) and extrapolate only between syncs. Either post the displayed unit price and reject the order if the server price differs by more than a tolerance, or change the "Lock price" wording to "price at time of order".

---

## Medium

### M1. `site.js` runs a hard-coded fake countdown on the Quick Sell details timer
- **Where:** `wwwroot/js/site.js:57-98` targets `#quickTimer` and counts down from a constant `24:15:30`. `_Layout.cshtml:352` loads `site.js` on every page, before the page's own script (`QuickSellDetails.cshtml:161,346-348`), and that script writes the same element every second.
- **Failure scenario:** On `/QuickSell/Details/{id}` two `setInterval` loops write `#quickTimer` each second, one with the real remaining time and one with `24:15:xx`. The timer flickers between them. On an expired listing it keeps showing a running `24:15:xx` alongside "EXPIRED".
- **Proposed fix:** Delete the `quickTimer` block from `site.js`. The `productSearch`/`.product-item` and `.add-cart-btn` blocks (`:3-46`) match no element anywhere in `Views/` and should go too (see L10).

### M2. Quick Sell purchase validation errors are never shown
- **Where:** `Controllers/QuickSellController.cs:171-184` adds ModelState errors ("Only N kg available", "at least 1 kg", and Required/Phone errors from `QuickSellPurchaseViewModel`) and re-renders the view. `Views/Ui/QuickSellDetails.cshtml:218-287` has no `asp-validation-summary`, no `asp-validation-for`, and does not include `_ValidationScriptsPartial`.
- **Failure scenario:** A customer requests 400 kg while 350 kg remain. The page re-renders with the form unchanged and no message, so the customer cannot tell why nothing happened.
- **Proposed fix:** Add `<div asp-validation-summary="All">` and per-field `asp-validation-for` spans, built with the view model and tag helpers, and include `_ValidationScriptsPartial`.

### M3. Add-to-cart errors are lost because Product Details does not render `TempData["Error"]`
- **Where:** `Controllers/CartController.cs:72-82` sets `TempData["Error"] = "Invalid quantity."` and redirects to `Products/Details`. `Views/Ui/ProductDetails.cshtml` contains no `TempData` rendering.
- **Failure scenario:** A customer enters 500 when stock is 120 (or bypasses the `max` attribute). They are bounced back to the same page with no message. The stale error then appears later on whichever page next renders `TempData["Error"]`, such as the catalog.
- **Proposed fix:** Render `TempData["Error"]` and `TempData["Success"]` in `ProductDetails.cshtml`, or better, in one shared partial in `_Layout.cshtml` so no page forgets. Also say *why* the quantity is invalid ("Only 120 kg in stock").

### M4. Product ownership hole: products with a null `FarmerId` can be edited and deleted by any farmer (confirmed)
- **Where:** `Controllers/ProductsController.cs:118`, `:151`, `:192` all use `if (!string.IsNullOrEmpty(product.FarmerId) && product.FarmerId != currentUserId)`. `Models/Product.cs:10` declares `string? FarmerId`.
- **Failure scenario:** A product row with `FarmerId = NULL`, for example a legacy row from before ownership existed or one inserted manually, passes all three checks. Any farmer can change its price or stock or soft-delete it. Today's `Create` always sets the id (`:90-92`), so only existing or imported rows are exposed.
- **Proposed fix:** Treat null as "not yours": `if (product.FarmerId != currentUserId)`. Make `FarmerId` required (non-nullable plus a migration that backfills or deactivates null rows). Extract an `IsOwnedBy(userId)` helper and test it.

### M5. Duplicate Quick Sell seeding, owner ids that are not foreign keys, and fake ids
- **Where:** `Controllers/QuickSellController.cs:29` calls `EnsureSeedDataAsync()` (defined at `:256-324`) on **every** marketplace request. It seeds owners `seed-farmer-1..3` (`:268,286,304`) that are not real users, and its content differs from `Data/DbInitializer.cs:215-278` (different farmer names and titles). Other fake fallback ids: `DbInitializer.cs:104` `"demo-farmer-id"`, `QuickSellController.cs:122` `"anonymous-farmer"`, `:191` `"guest-buyer"`. None of `Product.FarmerId`, `QuickSellListing.FarmerId`, `TransportTrip.FarmerId`, `TransportParticipant.FarmerId` or `QuickSellOrder.BuyerId` is a foreign key to `AspNetUsers` (see `Data/ApplicationDbContext.cs`, which configures no such relationship), although README's ER diagram (`README.md:161-167`) shows them as FKs.
- **Failure scenario:** If the listings table is ever emptied, the next `/QuickSell` hit creates three listings owned by nobody. Orders against them appear on no farmer's dashboard. Every page view also runs an extra `COUNT` query. If demo farmer creation fails in `DbInitializer` (for example the password policy changes), all seed data is owned by `"demo-farmer-id"` and no one can manage it.
- **Proposed fix:** Delete `EnsureSeedDataAsync` and leave seeding only in `DbInitializer`. Replace fake fallbacks with `Challenge()`/`Forbid()`. In `DbInitializer`, fail loudly if the demo farmer cannot be created. Add FK relationships from owner ids to `ApplicationUser` with `DeleteBehavior.Restrict` and a migration, and make the README ER diagram match.

### M6. Seed data goes stale and is never refreshed
- **Where:** `Data/DbInitializer.cs:215` and `:281` seed only when the tables are empty. Quick Sell `ExpiresAt = now + 30/36/42 h` (`:234,252,270`). Trip `DispatchDate = today + 1/2/3` (`:289,302,315`). Seeded listings also show `AvailableQuantity` 350 of 500 and 620 of 800 (`:229,247`) with no orders behind them.
- **Failure scenario:** Two days after the first `dotnet run`, `/QuickSell` is empty. After three days `/Transport` lists no trips. Because the tables are non-empty, a restart never re-seeds them, and the demo described in README:288-293 is gone. The farmer dashboard shows 150 kg "sold" with no matching order.
- **Proposed fix:** Seed relative to app start in a demo-only mode. For example, in Development, refresh `ExpiresAt`/`CreatedAt`/`DispatchDate` of seed rows when they have lapsed, or always seed with `AvailableQuantity == BulkQuantity`. Document the behaviour.

### M7. Transport suggestions: the nearby-date fallback is silent and trip dates are never shown
- **Where:** `Controllers/TransportController.cs:136-153` replaces the exact-date result with any future-dated trip and sets `ViewBag.IsNearbyDateMatch`, which no view reads (grep finds no hit in `Views/`). `Views/Ui/TransportSuggestions.cshtml:56-148` never displays `DispatchDate`. Neither query excludes trips the farmer has already joined. The exact-match query (`:126-131`) does not exclude past dates.
- **Failure scenario:** A farmer searches for City Hub on 10 Oct. No exact match exists, so a 15 Oct trip is shown with no date and no "nearby" notice. The farmer joins believing it leaves on the 10th. The list can also contain a trip they already joined, which then fails with "already joined". Searching a past date returns past trips, which can be joined until #10 lands.
- **Proposed fix:** Show `DispatchDate` on each suggestion and a banner when `IsNearbyDateMatch` is set. Exclude trips where the farmer is already a participant. Restrict both queries to `DispatchDate >= today` (this complements #10, which closes trips server-side).

### M8. Unit options are inconsistent across seed data, the form and README, and editing seeded milk loses its unit
- **Where:** `Views/Ui/ProductForm.cshtml:118-141` offers `kg | litre | dozen | unit`. `Data/DbInitializer.cs:177` seeds milk with `"L"`. `README.md:29` advertises `kg, L, bunches, cobs`.
- **Failure scenario:** A farmer opens Edit on "Fresh Farm Buffalo Milk". The `<select>` has no `L` option, so it shows "Select Unit" and posts `""`. `[Required]` fails, and the farmer must pick "litre", which silently renames the unit on all future orders.
- **Proposed fix:** Define one unit list, for example a `Units` static class or enum, used by the form, the seed and README. Migrate `"L"` to `"litre"`, or add `L` as the canonical value.

### M9. Quick Sell category list does not match the filter or the marketplace
- **Where:** `Views/Ui/QuickSellCreate.cshtml:34-37` offers `Vegetables, Fruits, Groceries, Dairy`. The marketplace filter `Views/Ui/QuickSell.cshtml:50` offers `All, Vegetables, Fruits, Groceries`. Products use `"Dairy Products"` (`ProductForm.cshtml:97`, `ProductCatalog.cshtml:82`).
- **Failure scenario:** A farmer creates a "Dairy" Quick Sell. No filter chip can select it, and it uses a different label from the "Dairy Products" product category.
- **Proposed fix:** Use one shared category list for both features, for example a `Categories` constant, and render the filter from it.

### M10. `DateTime.Now` and `DateTime.Today` are used everywhere instead of UTC (confirmed)
- **Where:** All timestamps: `Models/*.cs` defaults (`ApplicationUser.cs:22`, `CartItem.cs:21`, `Order.cs:41`, `Product.cs:37`, `QuickSellListing.cs:54,56,71,97,104`, `QuickSellOrder.cs:55`, `TransportParticipant.cs:27`, `TransportTrip.cs:39`). Controllers: `ProductsController.cs:94`, `CartController.cs:125`, `OrdersController.cs:160`, `QuickSellController.cs:31,138-139,207,251,263`, `TransportController.cs:25,67,83,145,264`, `AccountController.cs:73`. Seeds: `DbInitializer.cs` throughout. Views: `Transport.cshtml:121` (`DateTime.Today`).
- **Failure scenario:** On a UTC host, between 00:00 and 05:30 IST `DateTime.Today` is still yesterday. Yesterday's trips stay listed and joinable on Transport Index (`TransportController.cs:29`), and the date picker defaults to yesterday. Every displayed timestamp ("Placed on …", `OrderDetails.cshtml:23`) is 5.5 h off for Indian users. Moving the server between machines in different zones shifts all Quick Sell decay. This also causes H6.
- **Proposed fix:** Store `DateTime.UtcNow`, or `DateTimeOffset`. Inject `TimeProvider` so decay and expiry are testable (see #3). Convert to IST (`Asia/Kolkata`) only for display. Use an IST "today" for date-only comparisons such as dispatch dates.

### M11. Dashboard aggregates ignore order status
- **Where:** `Controllers/UiController.cs:84-86` (farmer earnings sum every `QuickSellOrder` and `OrderItem`) and `:128-129` (customer `TotalSpent` sums every order. "Active" counts Quick Sell orders by `Status == "Confirmed"` and retail orders by `!= Delivered/Cancelled`).
- **Failure scenario:** Once #9 adds Cancelled, a cancelled ₹2,000 order still counts in the farmer's "Total Sales" and the customer's "Total Spent". The two order types use different status vocabularies ("Confirmed" vs "Placed"), so after #9 renames them the Quick Sell "active" count drops to 0.
- **Proposed fix:** After #9, compute totals over non-cancelled orders only. Use the shared status constants from #9 for "active" (`Placed`). Put the metric logic in a testable service.

### M12. Fare shares are rounded per participant and can fail to sum to the vehicle cost
- **Where:** `Controllers/TransportController.cs:340-349` computes `TotalVehicleCost * (weight / combinedWeight)` at full decimal precision. `Models/TransportParticipant.cs:22-23` stores `[Precision(18, 2)]`, so each share is cut to 2 dp on save.
- **Failure scenario:** The cost is ₹1,000 and three participants carry 100 kg each. Each share is 333.33, which sums to ₹999.99, so ₹0.01 is never collected. Other splits can overshoot. Shares also change for existing participants every time someone joins, and nothing records that.
- **Proposed fix:** Round each share to 2 dp explicitly (`Math.Round(..., 2, MidpointRounding.AwayFromZero)`) and assign the remainder (cost − Σ rounded shares) to the host or the largest participant so the shares sum exactly. Add a unit test for the 1000/3 case.

### M13. Missing string-length validation turns long input into HTTP 500
- **Where:** `ViewModels/QuickSellCreateViewModel.cs:31` `Location` has no length limit, but the column is `nvarchar(150)` (`Models/QuickSellListing.cs:17`). `ViewModels/QuickSellPurchaseViewModel.cs:17,22,26,30` (`BuyerName`, `BuyerPhone`, `DeliveryAddress`, `City`) have no limits, but the columns are 100/20/200/100 (`Models/QuickSellOrder.cs:18-34`). `[Phone]` accepts 20+ characters. `CheckoutViewModel` and `Order` (`Models/Order.cs:13-25`) have no lengths at all and map to `nvarchar(max)`. `ProfileViewModel.City/District` have no length limit, but the columns are 100 (`ApplicationUser.cs:12-16`).
- **Failure scenario:** A customer types a 250-character warehouse address on a Quick Sell purchase. SQL Server raises a truncation error inside `SaveChangesAsync` (`QuickSellController.cs:219`) and the user gets an error page instead of a validation message.
- **Proposed fix:** Mirror every entity `[StringLength]` on the matching view model, and add sensible lengths to `Order`/`CheckoutViewModel` (with a migration).

---

## Low

### L1. Decimal formatting and parsing depend on the server culture
- **Where:** Numbers rendered straight into JS: `Views/Ui/QuickSellDetails.cshtml:301-303,306,316`. Number inputs given culture-formatted values: `Cart.cshtml:96`, `TransportSuggestions.cshtml:143`. Form model binding parses decimals with `CurrentCulture`. No `UseRequestLocalization` in `Program.cs`.
- **Failure scenario:** On a host with a comma-decimal culture (for example `de-DE`), `const startPrice = 30,00;` is a JS syntax error and the whole ticker script dies. The browser posts `12.5` from `<input type=number>`, which binds as `125`.
- **Proposed fix:** Pin the culture (`UseRequestLocalization` with `en-IN` and invariant number parsing), and emit JS numbers with `ToString(CultureInfo.InvariantCulture)` or `@Json.Serialize(...)`.

### L2. `ApplicationUser.UserRole` duplicates Identity roles (confirmed)
- **Where:** `Models/ApplicationUser.cs:18-20`. Login "self-heals" roles from it but never removes stale ones (`AccountController.cs:149-156`). Login redirects by `UserRole` rather than the real role (`:173-184`). Profile shows it (`:221`), and the Profile POST does not round-trip it, so after a validation failure the badge reads " Account" (`:233-236`, `Views/Account/Profile.cshtml:24`). `DbInitializer.cs:103` finds farmers through it.
- **Failure scenario:** If `AspNetUserRoles` and `UserRole` diverge (manual fix, future admin tool), the user is redirected to a dashboard they get AccessDenied on. Login can also silently grant a second role.
- **Proposed fix:** Remove `UserRole` (with a migration) and use `UserManager.GetRolesAsync`/`User.IsInRole` everywhere. Do this alongside #4, which already touches the role list.

### L3. The cart shows deleted products and stale quantities, and the checkout error does not say which item
- **Where:** `Controllers/CartController.cs:27-33` loads cart items without filtering `Product.IsActive`. `Views/Ui/Cart.cshtml:6-8,54-139` prices them into the subtotal. `OrdersController.cs:125-137` throws a generic "A product is no longer available."
- **Failure scenario:** A farmer deletes a product that is in a customer's cart. The cart still shows it, priced. Checkout fails, and the customer has to guess which line to remove. A line whose quantity now exceeds stock looks normal until checkout.
- **Proposed fix:** Flag unavailable and over-stock lines in the cart, exclude them from the subtotal, and block "Proceed" until they are resolved. Name the product in checkout errors.

### L4. Checkout shows raw exception messages to the user
- **Where:** `Controllers/OrdersController.cs:206-212` catches `Exception` and puts `ex.Message` into ModelState.
- **Failure scenario:** Any `DbUpdateException` or SQL error text (table and constraint names) is shown on the checkout page.
- **Proposed fix:** Use a domain exception type (for example `CheckoutException`) for the expected stock and availability errors. Log anything else and show a generic message.

### L5. Business constants are hard-coded and duplicated (confirmed)
- **Where:** 48 h: `QuickSellController.cs:137-139`, `Models/QuickSellListing.cs:52,56`, `QuickSellDetails.cshtml:112`, `QuickSellCreate.cshtml:128,136`, plus the TempData text at `QuickSellController.cs:147`. ₹30: `OrdersController.cs:144,252`, `Cart.cshtml:166,178`, `checkout.cshtml:10`. Low-stock threshold 20 (unit-agnostic): `FarmerDashboard.cshtml:147`. Preset 50 kg: `QuickSellController.cs:69`, `QuickSellDetails.cshtml:6`.
- **Failure scenario:** Changing the delivery charge in the controller leaves the cart page showing ₹30, so the cart and checkout totals disagree. #8 will hit exactly this.
- **Proposed fix:** Centralize these in an options class (`FarmGridOptions { QuickSellDurationHours, DeliveryChargePerOrder }`) bound from `appsettings.json` and pass the values to the views. Coordinate with #8.

### L6. Quick Sell quantity granularity strands remainders below 1 kg
- **Where:** `ViewModels/QuickSellPurchaseViewModel.cs:11` `[Range(1, …)]` on a decimal. The input is `step="1"` (`QuickSellDetails.cshtml:228-231`). `BulkQuantity` is also decimal with `step=1` (`QuickSellCreate.cshtml:45`). The server accepts 1.5.
- **Failure scenario:** A customer buys 349.5 of 350 kg through a crafted POST. The remaining 0.5 kg can never be bought (below the minimum of 1), and the listing stays live as "0.5 kg left" until it expires. The "All (350.5 kg)" preset also fails the browser's `step` validation.
- **Proposed fix:** Choose one rule: integer kg (`int` or a whole-number check on both create and purchase), or allow the final remainder below 1 kg to be bought.

### L7. Trip creation accepts past dates and is not atomic
- **Where:** `Controllers/TransportController.cs:47-91` has no `DispatchDate >= today` check. The trip insert (`:72`), host participant insert (`:89`) and fare recalculation (`:91`) are three separate `SaveChanges` calls with no transaction.
- **Failure scenario:** A farmer picks last week by mistake. The trip saves "successfully" and never appears anywhere except MyTrips. A failure after the first save leaves a trip with no host participant.
- **Proposed fix:** Validate the dispatch date on create (complements #10). Wrap the create in a transaction, or add the host participant to `trip.Participants` before a single `SaveChanges`.

### L8. The farmer dashboard shows soft-deleted products as active, and Edit works on deleted products
- **Where:** `Controllers/UiController.cs:33-36` has no `IsActive` filter. `Views/Ui/FarmerDashboard.cshtml:143-154` derives the badge from stock only. `ProductsController.cs:109-110,142-143` (Edit uses `FindAsync` with no `IsActive` check).
- **Failure scenario:** A farmer deletes a product with 50 kg in stock. The dashboard still lists it with a green "Active" badge, and editing it changes nothing visible because it stays hidden from the catalog.
- **Proposed fix:** Badge `IsActive == false` as "Removed" (or hide it), and reject Edit on removed products. Follows the `IsActive` = deleted rule decided in #6.

### L9. Transport Index shows "Request to Join" on the farmer's own trips and on trips already joined
- **Where:** `Controllers/TransportController.cs:27-32` (no `FarmerId` or participant exclusion). `Views/Ui/Transport.cshtml:206-213`.
- **Failure scenario:** The demo farmer, who owns all seeded trips, clicks "Request to Join". Suggestions excludes own trips (`:131`), so the result is "No matching trips found" or an unrelated nearby trip.
- **Proposed fix:** Exclude own and joined trips from the open list, or render them as "Your trip" / "Joined".

### L10. Dead code and dead views (confirmed)
- **Where:** 13 redirect-only actions in `Controllers/UiController.cs:145-220`. Nothing links to them: the only `asp-controller="UI"` targets are `FarmerDashboard`/`CustomerDashboard`. Also: `Views/Ui/Profile.cshtml` (static mock with a `<form>` that has no action, never rendered), `Views/Shared/_LoginPartial.cshtml` (not referenced), `site.js:3-46` (selectors match nothing), the unused `isFarmerOwner` (`QuickSellDetails.cshtml:8`, which also compares an email to a display name), and the unused `data-start-price/floor-price/duration/created` attributes (`QuickSell.cshtml:146-150`).
- **Failure scenario:** Maintainers edit the mock `Views/Ui/Profile.cshtml` believing it is live. The redirects widen the route surface.
- **Proposed fix:** Delete all of it.

### L11. View paths do not match the folder and file casing (`~/Views/UI/...` vs `Views/Ui/`, `Checkout.cshtml` vs `checkout.cshtml`)
- **Where:** Every explicit view path in `Products/Cart/Orders/QuickSell/TransportController` uses `~/Views/UI/`. `OrdersController.cs:73,100,111,217` use `Checkout.cshtml`, but the file is `Views/Ui/checkout.cshtml`.
- **Verified behaviour:** This does **not** break today, including on Linux. Views are compiled at build time, and the compiled-view lookup in `DefaultViewCompiler` uses a dictionary built with `StringComparer.OrdinalIgnoreCase` ([aspnetcore source](https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Razor/src/Compilation/DefaultViewCompiler.cs)). It would break on a case-sensitive file system if Razor runtime compilation or file-based view lookup were enabled.
- **Proposed fix:** Rename the file to `Checkout.cshtml` and use `~/Views/Ui/...` everywhere, or move the views to their controller folders and call `View()`.

### L12. Login reveals whether an account exists and has no lockout
- **Where:** `Controllers/AccountController.cs:142-146` returns "No user found with this email." `:162` sets `lockoutOnFailure: false`.
- **Failure scenario:** An attacker enumerates registered emails, then brute-forces passwords with no throttle.
- **Proposed fix:** Return one generic "Invalid email or password" message and enable `lockoutOnFailure: true`.

### L13. Order numbers are formatted inconsistently
- **Where:** `Orders.cshtml:92` and `OrderDetails.cshtml:3,12,21` show `#FG0001`. `CustomerDashboard.cshtml:115` shows `#1`. Quick Sell orders show `#QS-1` (`CustomerDashboard.cshtml:186`).
- **Failure scenario:** A customer quoting "#12" from the dashboard cannot find it on the invoice page, which says "#FG0012".
- **Proposed fix:** Add one `OrderNumber` display helper and use it everywhere.

### L14. Catalog UX inconsistencies: anonymous Add to Cart, category not kept, placeholder farmer names
- **Where:** `ProductCatalog.cshtml:223-237` shows Add to Cart to anonymous users. The POST triggers a login challenge, and the return URL is a GET to `/Cart/Add`, which `CartController.cs:41-44` redirects to the catalog, so the item is dropped. The category `<select>` does not keep the selected value (`ProductCatalog.cshtml:63-86`). "FarmGrid Farmer" appears instead of the real farmer name (`ProductCatalog.cshtml:160`, `ProductDetails.cshtml:37`, `TransportSuggestions.cshtml:73`). Farmers see Edit/Delete on every product, including other farmers' (`ProductCatalog.cshtml:200-221`, `ProductDetails.cshtml:68-86`). The server rejects those actions, but the UI offers them.
- **Proposed fix:** For anonymous users, link to login with `returnUrl` set to the product page. Keep the filter value selected. Show the farmer's `FullName`. Show Edit/Delete only when `product.FarmerId == currentUserId`.

### L15. README drift (excluding B2B, Online payment and statuses, which are covered by #4, #7 and #9)
- `README.md:29` lists units `kg, L, bunches, cobs`. The form offers `kg, litre, dozen, unit` (M8).
- `README.md:31` "Atomic stock reservations". The cart reserves nothing, and checkout is not concurrency-safe (H3).
- `README.md:38` "synchronizing … to prevent race conditions". The sync is overwritten every tick, and the server has no race protection (H2, H6).
- `README.md:45,135` "automatic nearby-date fallback". It is applied silently and no dates are shown (M7).
- `README.md:138` "In an atomic transaction, capacity is decremented". The check happens outside the transaction (H4).
- `README.md:144` "Total Combined Earnings". Includes the fake ₹18,450 and other farmers' data (H5).
- `README.md:161-167` ER diagram shows owner FKs that do not exist (M5).
- `README.md:57` says C# 13. .NET 10 defaults to C# 14.
- `README.md:324` links `LICENSE`, but there is no LICENSE file in the repo.
- **Proposed fix:** Fold into the "Final README/doc sync" item already noted on the map, fixing each line as its code ticket lands.

---

## Suspected items: verdicts

| Suspected | Verdict |
| --- | --- |
| Quick Sell purchase: no transaction or concurrency token (oversell) | **Confirmed** → H2 |
| Checkout and trip join lack rowversion | **Confirmed** → H3, H4 (also a missing unique participant index) |
| Null-`FarmerId` products editable or deletable by any farmer | **Confirmed** → M4 (only legacy or manual rows are exposed today) |
| Duplicate Quick Sell seeding with `seed-farmer-N` ids | **Confirmed** → M5 |
| `DateTime.Now` vs UTC | **Confirmed** → M10, and causes H6 |
| `ApplicationUser.UserRole` duplicating Identity roles | **Confirmed** → L2 |
| Dead redirect actions in `UiController` | **Confirmed** → L10 |
| Hardcoded constants (48 h, ₹30) | **Confirmed** → L5 |
| JS ticker vs server price formula mismatch | **Confirmed, but not in the formula itself**: the formulas match. The mismatch comes from timezone parsing, an overwritten sync and rounding mode → H6. Also the `site.js` timer collision → M1 |
| Fare shares not summing to total cost | **Confirmed** → M12 |
| Decimal and validation gaps | **Confirmed** → M13, L1, L6 |
| Host cargo vs capacity | **Rejected.** `AvailableCapacityKg` is *spare* capacity, separate from host cargo, which matches README:131 and the form labels. Nothing compares them, and nothing needs to. There is no total vehicle capacity field, which is a possible feature, not a bug. The real capacity bug is the race in H4 and the over-posting in H1 |
| Trip suggestions date logic | **Confirmed** → M7 |
| Cart quantity vs units | **Confirmed in part.** Cart and Details allow 0.01 steps for `dozen`/`unit` products (`ProductDetails.cshtml:105-111`, `Cart.cshtml:94-101`, `CartController` accepts any decimal). Folded into M8: once the unit list exists, attach a step to each unit |
| Missing anti-forgery or authorization | **Rejected.** Every `[HttpPost]` has `[ValidateAntiForgeryToken]`, and every mutating action has `[Authorize(Roles=…)]`. Optional hardening: a global `AutoValidateAntiforgeryTokenAttribute` filter. The actual authorization-adjacent holes are H1 (over-posting) and M4 |
| Wrong view paths and Linux case sensitivity | **Downgraded** → L11. Verified that build-time compiled views are looked up case-insensitively |
| Dashboard metric errors | **Confirmed** → H5 (fake and foreign data), M11 (status ignored) |
| N+1 or unbounded queries causing incorrect results | **Rejected as a correctness issue.** There are no N+1 patterns: `Include` is used throughout. Dashboard "Recent" lists are unbounded (`UiController.cs:45-57,116-126`), which is a performance and UX problem only. Fix it by adding `.Take(n)` while touching M11 |

## Interactions with existing tickets
- **#6:** L8 relies on "`IsActive` = deleted". Remove the sell-out clamp-and-deactivate code in H2/H3 when implementing #6.
- **#8:** H3 and L5 touch the same checkout loop and ₹30 constant. Implement them together.
- **#9:** M11 depends on its status constants. Restoring stock on cancel needs the same concurrency protection as H2/H3.
- **#10:** M7 and L7 cover the query and create side. #10 covers server-side closing and join rejection.
- **#4:** L2 (dropping `UserRole`) fits naturally alongside role cleanup.
- **#3:** M10 (`TimeProvider`), M12 and H2–H4 are the natural first tests.
