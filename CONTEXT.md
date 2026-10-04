# FarmGrid

A marketplace where farmers sell produce directly to customers, clear perishable surplus through time-decaying prices, and pool vehicles to reach markets.

## Language

### People

**Farmer**:
A user who grows and sells produce, lists Quick Sell surplus, and hosts or joins Trips.
_Avoid_: Seller, vendor, host (except for a Trip's host)

**Customer**:
A user who buys produce, either from the catalog or from Quick Sell. The only buying role.
_Avoid_: Buyer, B2B buyer, wholesale buyer, client

**Location**:
A user's Gujarat city and the district it belongs to, chosen from a fixed list; the district always follows from the city.
_Avoid_: Address, region, state

### Selling

**Product**:
A farmer's standing catalog item, sold at a fixed unit price from a stock quantity.
_Avoid_: Listing, item

**Available**:
A Product customers can see and buy: not deleted by its Farmer and with stock above zero. A Quick Sell lot is available when it also has not expired. Selling out makes it unavailable; restocking makes it available again.
_Avoid_: Active, in catalog, live

**Order**:
A customer's purchase of catalog Products from a single Farmer. One checkout produces one Order per Farmer in the cart.
_Avoid_: Purchase, transaction

**Order status**:
The stage of an Order or Quick Sell order: **Placed**, then either **Delivered** or **Cancelled**, set only by the owning Farmer. Delivered and Cancelled are final; cancelling returns the quantity to stock.
_Avoid_: Confirmed, Shipped, Pending

**Quick Sell**:
Surplus clearance: a farmer offers a large lot of perishable produce whose price falls steadily from a starting price to a floor price over a fixed window.
_Avoid_: Wholesale, bulk deal, B2B, Dutch auction

**Floor price**:
The lowest price a Quick Sell lot's price can fall to.
_Avoid_: Minimum price, reserve

### Transport

**Market**:
A real agricultural market (APMC yard) in Gujarat that a Trip can go to, chosen from a fixed list, with its city and district.
_Avoid_: Destination, mandi (as a free-text name), hub

**Origin**:
Where a Trip starts: always the host Farmer's current Location.
_Avoid_: Source, pickup point, starting point

**Trip**:
A farmer's vehicle journey to a market, with spare capacity that other farmers can book. A Trip is open through its dispatch date and closed once that date has passed; a closed Trip cannot be found or joined.
_Avoid_: Ride, booking, pool

**Fare share**:
A Trip participant's part of the vehicle cost, in proportion to their cargo weight.
_Avoid_: Fare, split
