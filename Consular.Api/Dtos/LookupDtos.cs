using System.ComponentModel.DataAnnotations;
using Consular.Shared.Enums;

namespace Consular.Api.Dtos;

public record TypeServiceDto(Guid Id, string Code, string Libelle, TypeServiceCategorie Categorie, string? Description, decimal MontantFrais, List<string> DocumentsRequisFr, List<string> DocumentsRequisEn);

public record StatutDto(Guid Id, string Code, string Libelle, int Ordre, bool EstFinal);

public record RegionLookupDto(Guid Id, string Code, int Valeur, string LibelleFr, string LibelleEn, int Ordre);

// Admin variants include Actif — the public lookups endpoints only ever return active rows,
// but a Nord account managing the system needs to see inactive ones too.
public record TypeServiceAdminDto(Guid Id, string Code, string Libelle, TypeServiceCategorie Categorie, string? Description, bool Actif, decimal MontantFrais);

public record StatutAdminDto(Guid Id, string Code, string Libelle, int Ordre, bool EstFinal, bool Actif);

// Weekly recurring appointment slots (see AppointmentSlotTemplate), scoped per region AND
// service category (a Passeport slot and a Visa slot at the same region/day/time are different
// resources). "Weekly template" (below) tells the applicant-facing calendar which weekdays are
// open at all, before any date is picked; "availability" is the live per-date remaining-capacity
// list once a date is chosen.
public record AppointmentSlotWeeklyTemplateDto(DayOfWeek DayOfWeek, TimeOnly StartTime, int CapaciteMax);

public record AppointmentSlotAvailabilityDto(TimeOnly StartTime, int CapaciteMax, int Remaining);

public record AppointmentSlotTemplateAdminDto(Guid Id, int Region, TypeServiceCategorie Categorie, DayOfWeek DayOfWeek, TimeOnly StartTime, int CapaciteMax, bool Actif);

public record CreateAppointmentSlotTemplateAdminDto(
    [Range(0, 1, ErrorMessage = ValidationMessages.Range)] int Region,
    TypeServiceCategorie Categorie,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    [Range(1, int.MaxValue, ErrorMessage = ValidationMessages.Range)] int CapaciteMax
);

// Region/Categorie/DayOfWeek/StartTime are the natural key and stay immutable once created —
// same convention as Statut.Code/TypeService.Code never changing via their own Update DTOs
// above. To move a slot to a different day/time/category, delete it and create a new one.
public record UpdateAppointmentSlotTemplateAdminDto([Range(1, int.MaxValue, ErrorMessage = ValidationMessages.Range)] int CapaciteMax, bool Actif);
