using Consular.Shared.Entities;

namespace Consular.Api.Services;

// Pure computation over already-fetched rows — no AppDbContext here, so this is unit-testable
// the same way WorkingDays/DemandeWorkflowRules/ReportCalculations are (see
// AppointmentSlotAvailabilityTests): callers (LookupsController, DemandesController.Create)
// pre-filter AppointmentSlotTemplate rows to one Region+Categorie+DayOfWeek and booked
// Demande.RendezVousBiometrieAt values to that same Region+Categorie+calendar date, then hand
// both lists here.
public static class AppointmentSlotAvailability
{
    public record SlotAvailability(TimeOnly StartTime, int CapaciteMax, int Remaining);

    public static List<SlotAvailability> ComputeForDate(
        IEnumerable<AppointmentSlotTemplate> templatesForRegionAndDay,
        IEnumerable<DateTime> bookedForRegionAndDate)
    {
        var bookedByTime = bookedForRegionAndDate
            .GroupBy(dt => TimeOnly.FromDateTime(dt))
            .ToDictionary(g => g.Key, g => g.Count());

        return templatesForRegionAndDay
            .OrderBy(t => t.StartTime)
            .Select(t =>
            {
                var booked = bookedByTime.GetValueOrDefault(t.StartTime);
                return new SlotAvailability(t.StartTime, t.CapaciteMax, Math.Max(0, t.CapaciteMax - booked));
            })
            .ToList();
    }

    // Re-validates one specific requested time — the source of truth at booking time, since the
    // availability a client fetched earlier (to render the picker) may be stale by the time it
    // actually submits.
    public static bool TryValidateSlot(
        IEnumerable<AppointmentSlotTemplate> templatesForRegionAndDay,
        IEnumerable<DateTime> bookedForRegionAndDate,
        TimeOnly requestedTime,
        out int remaining)
    {
        var match = ComputeForDate(templatesForRegionAndDay, bookedForRegionAndDate)
            .FirstOrDefault(a => a.StartTime == requestedTime);
        remaining = match?.Remaining ?? 0;
        return match is not null && match.Remaining > 0;
    }
}
