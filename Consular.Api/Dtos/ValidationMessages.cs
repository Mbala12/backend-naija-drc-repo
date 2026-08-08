namespace Consular.Api.Dtos;

// Centralized French replacements for ASP.NET Core's built-in DataAnnotations default messages.
// Those defaults are English and, unlike this codebase's own hand-written ValidationProblem/
// Problem messages, don't follow CultureInfo.DefaultThreadCurrentCulture (see Program.cs) in this
// runtime — the container's .NET install has no French satellite resources for
// System.ComponentModel.DataAnnotations — so every [Required]/[EmailAddress]/[Phone]/
// [StringLength]/[Range] attribute needs its ErrorMessage set explicitly instead. {0}/{1}/{2}
// are the same positional placeholders (field display name, max, min) ValidationAttribute
// already substitutes for the English defaults.
internal static class ValidationMessages
{
    public const string Required = "Le champ {0} est requis.";
    public const string Email = "Le champ {0} n'est pas une adresse email valide.";
    public const string Phone = "Le champ {0} n'est pas un numéro de téléphone valide.";
    public const string StringLength = "Le champ {0} doit contenir entre {2} et {1} caractères.";
    public const string Range = "Le champ {0} doit être compris entre {1} et {2}.";
}
