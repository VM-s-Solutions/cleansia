namespace Cleansia.Core.Domain.Internationalization;

public class Translation
{
    public string Name { get; set; }
    public string Description { get; set; }

    /// <summary>
    /// Optional. Only <see cref="Cleansia.Core.Domain.Packages.Package"/> sets it today — the
    /// short line a package card leads with, above its name. It lives on the shared type rather
    /// than in a package-only translation because translations persist as one JSON column, so a
    /// nullable field here costs no schema and no migration, while a parallel type would
    /// duplicate the whole translation apparatus for a single string.
    /// </summary>
    public string? Tagline { get; set; }
}