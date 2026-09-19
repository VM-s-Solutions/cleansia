using System.Text.RegularExpressions;

namespace Cleansia.Core.AppServices.Features.Gdpr;

/// <summary>
/// The note a failed erasure leaves on its request row. The innermost exception is the one that names the
/// cause (a <c>DbUpdateException</c> only says to look inside). The row is read by admins and kept for
/// years, so any e-mail-shaped token a message happens to carry is blanked before it is stored — the
/// subject's address must never land here.
/// </summary>
public static partial class ErasureFailureNote
{
    public const string Redacted = "[redacted]";

    public static string Describe(Exception exception)
    {
        var root = exception;
        while (root.InnerException is not null)
        {
            root = root.InnerException;
        }

        return $"{root.GetType().Name}: {EmailShaped().Replace(root.Message, Redacted)}";
    }

    [GeneratedRegex(@"\S+@\S+")]
    private static partial Regex EmailShaped();
}
