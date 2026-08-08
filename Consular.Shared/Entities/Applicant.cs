namespace Consular.Shared.Entities;

// A member of the public who submits and tracks consular requests. Deliberately carries no
// role/region — that's what distinguishes an Applicant from a User (see User.cs): an Applicant
// account only ever grants access to that person's own requests, never to staff/admin tooling.
public class Applicant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty; // E.164 format, e.g. +2348012345678
    public string Nationalite { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Null means this is a beneficiary-only record (someone submitted a demande on their
    // behalf and they've never registered themselves) — they can't log in until they do.
    public string? MotDePasseHash { get; set; }

    public ICollection<Demande> Demandes { get; set; } = new List<Demande>();
}
