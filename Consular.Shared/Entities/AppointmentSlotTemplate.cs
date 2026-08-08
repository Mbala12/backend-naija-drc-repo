using Consular.Shared.Enums;

namespace Consular.Shared.Entities;

// Admin-configurable weekly recurring appointment slot — one row per region/category/day-of-week/
// time combination (e.g. "Lagos, Passeport, Monday, 09:00, capacity 5"). Categorie scopes capacity
// independently per service category — a Passeport slot and a Visa slot at the same region/day/
// time are different resources, not one shared pool. Used to build the applicant-facing calendar/
// slot picker at submission time (any category) and to validate/count bookings against
// Demande.RendezVousBiometrieAt (see AppointmentSlotAvailability). Despite the historical field
// name on Demande, this template — and the appointments booked against it — now cover every
// service category, not just passport biometrics.
public class AppointmentSlotTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Region Region { get; set; }
    public TypeServiceCategorie Categorie { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public int CapaciteMax { get; set; }
    public bool Actif { get; set; } = true;
}
