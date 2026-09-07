namespace Cleansia.Core.AppServices.Features.Packages.DTOs;

/// <summary>
/// A package's translation carries a third string the shared
/// <see cref="Services.CreateService.TranslationInput"/> does not: the short line the card leads
/// with. It is its own type rather than a field added to that one so the Service contract, and
/// every client generated from it, stays unchanged.
/// </summary>
public record PackageTranslationInput(string Name, string Description, string? Tagline);
