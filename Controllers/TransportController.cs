using FarmGrid.Data;
using FarmGrid.Models;
using FarmGrid.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FarmGrid.Controllers
{
    [Authorize]
    public class TransportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TransportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Transport landing page
        // Do NOT show all trips automatically.
        public IActionResult Index()
        {
            return View("~/Views/UI/Transport.cshtml");
        }

        // Open Create Transport page
        [HttpGet]
        public IActionResult Create()
        {
            return View("~/Views/UI/TransportCreate.cshtml");
        }

        // Create new transport trip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TransportTrip trip)
        {
            var farmerId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (farmerId == null)
            {
                return Challenge();
            }

            ModelState.Remove(nameof(TransportTrip.FarmerId));

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/UI/TransportCreate.cshtml",
                    trip);
            }

            trip.FarmerId = farmerId;
            trip.CreatedAt = DateTime.Now;
            trip.IsActive = true;

            _context.TransportTrips.Add(trip);

            await _context.SaveChangesAsync();

            var hostParticipant =
                new TransportParticipant
                {
                    TransportTripId = trip.Id,
                    FarmerId = farmerId,
                    CargoWeightKg =
                        trip.HostCargoWeightKg,
                    FareShare = 0,
                    IsHost = true,
                    JoinedAt = DateTime.Now
                };

            _context.TransportParticipants.Add(
                hostParticipant);

            await _context.SaveChangesAsync();

            await RecalculateFareShares(trip.Id);

            TempData["Success"] =
                "Transport trip created successfully.";

            return RedirectToAction(nameof(Index));
        }

        // Search matching trips
        [HttpGet]
        public async Task<IActionResult> Suggestions(
            string destinationMarket,
            DateTime dispatchDate,
            decimal requiredWeight)
        {
            var farmerId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (farmerId == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(destinationMarket) ||
                requiredWeight <= 0)
            {
                TempData["Error"] =
                    "Please enter valid transport search details.";

                return RedirectToAction(nameof(Index));
            }

            var matches =
                await _context.TransportTrips
                    .Include(t => t.Participants)
                    .Where(t =>
                        t.IsActive &&
                        t.DestinationMarket ==
                            destinationMarket &&
                        t.DispatchDate.Date ==
                            dispatchDate.Date &&
                        t.AvailableCapacityKg >=
                            requiredWeight &&
                        t.FarmerId != farmerId)
                    .OrderBy(t =>
                        t.TotalVehicleCost)
                    .ToListAsync();

            ViewBag.RequiredWeight =
                requiredWeight;

            ViewBag.DestinationMarket =
                destinationMarket;

            ViewBag.DispatchDate =
                dispatchDate;

            return View(
                "~/Views/UI/TransportSuggestions.cshtml",
                matches);
        }

        // Join existing trip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(
            TransportJoinViewModel model)
        {
            var farmerId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (farmerId == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] =
                    "Invalid cargo weight.";

                return RedirectToAction(nameof(Index));
            }

            var trip =
                await _context.TransportTrips
                    .Include(t => t.Participants)
                    .FirstOrDefaultAsync(t =>
                        t.Id == model.TripId &&
                        t.IsActive);

            if (trip == null)
            {
                return NotFound();
            }

            if (trip.FarmerId == farmerId)
            {
                TempData["Error"] =
                    "You cannot join your own trip.";

                return RedirectToAction(nameof(Index));
            }

            if (trip.Participants.Any(p =>
                    p.FarmerId == farmerId))
            {
                TempData["Error"] =
                    "You have already joined this trip.";

                return RedirectToAction(nameof(Index));
            }

            if (model.CargoWeightKg <= 0)
            {
                TempData["Error"] =
                    "Cargo weight must be greater than zero.";

                return RedirectToAction(nameof(Index));
            }

            if (model.CargoWeightKg >
                trip.AvailableCapacityKg)
            {
                TempData["Error"] =
                    "Not enough available capacity.";

                return RedirectToAction(nameof(Index));
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var participant =
                    new TransportParticipant
                    {
                        TransportTripId =
                            trip.Id,

                        FarmerId =
                            farmerId,

                        CargoWeightKg =
                            model.CargoWeightKg,

                        FareShare =
                            0,

                        IsHost =
                            false,

                        JoinedAt =
                            DateTime.Now
                    };

                _context.TransportParticipants.Add(
                    participant);

                trip.AvailableCapacityKg -=
                    model.CargoWeightKg;

                await _context.SaveChangesAsync();

                await RecalculateFareShares(
                    trip.Id);

                await transaction.CommitAsync();

                TempData["Success"] =
                    "You successfully joined the transport trip.";

                return RedirectToAction(
                    nameof(MyTrips));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Show only trips created or joined by current farmer
        public async Task<IActionResult> MyTrips()
        {
            var farmerId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (farmerId == null)
            {
                return Challenge();
            }

            var trips =
                await _context.TransportTrips
                    .Include(t => t.Participants)
                    .Where(t =>
                        t.FarmerId == farmerId ||
                        t.Participants.Any(p =>
                            p.FarmerId ==
                                farmerId))
                    .OrderByDescending(t =>
                        t.DispatchDate)
                    .ToListAsync();

            return View(
                "~/Views/UI/MyTransportTrips.cshtml",
                trips);
        }

        private async Task RecalculateFareShares(
            int tripId)
        {
            var trip =
                await _context.TransportTrips
                    .Include(t => t.Participants)
                    .FirstAsync(t =>
                        t.Id == tripId);

            var combinedWeight =
                trip.Participants.Sum(p =>
                    p.CargoWeightKg);

            if (combinedWeight <= 0)
            {
                return;
            }

            foreach (var participant
                     in trip.Participants)
            {
                participant.FareShare =
                    trip.TotalVehicleCost *
                    (
                        participant.CargoWeightKg /
                        combinedWeight
                    );
            }

            await _context.SaveChangesAsync();
        }
    }
}