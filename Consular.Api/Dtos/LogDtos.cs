using System.ComponentModel.DataAnnotations;

namespace Consular.Api.Dtos;

// Frontend runtime errors (uncaught exceptions, unhandled promise rejections, React render
// errors) reported by the browser — see LogsController. Level distinguishes an outright crash
// from a lesser warning, but everything here originates client-side, never from user input on
// a form, so none of this is meant to be user-facing.
public record ClientErrorDto(
    [Required] string Message,
    string? Stack,
    string? Url,
    string? UserAgent,
    string? Level = "error"
);
