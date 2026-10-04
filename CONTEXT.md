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

### Selling

**Product**:
A farmer's standing catalog item, sold at a fixed unit price from a stock quantity.
_Avoid_: Listing, item

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

**Trip**:
A farmer's vehicle journey to a market, with spare capacity that other farmers can book. A Trip closes once its dispatch date has passed.
_Avoid_: Ride, booking, pool

**Fare share**:
A Trip participant's part of the vehicle cost, in proportion to their cargo weight.
_Avoid_: Fare, split
