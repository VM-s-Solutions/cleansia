namespace Cleansia.Core.Blobs.Abstractions;

/// <summary>
/// An issued document is never overwritten: every render of it is a new file, numbered after the first
/// (<c>INV-2026-000017.pdf</c>, then <c>INV-2026-000017-2.pdf</c>, <c>-3</c> …), and its row points at the
/// newest (→ /decisions/adr-0083).
/// </summary>
public static class DocumentVersions
{
    private const int MaxVersions = 100;

    public static string VersionName(string firstName, int version) =>
        version == 1
            ? firstName
            : $"{firstName[..^Path.GetExtension(firstName).Length]}-{version}{Path.GetExtension(firstName)}";

    /// <summary>
    /// Writes the content under the lowest version name nobody holds and returns that name. The store
    /// arbitrates each name, so two renders at once never land on one file and an existing file is never
    /// replaced.
    /// </summary>
    public static async Task<string> UploadNewVersionAsync(
        this IBlobContainerClient client, string firstName, byte[] content, CancellationToken cancellationToken)
    {
        for (var version = 1; version <= MaxVersions; version++)
        {
            var name = VersionName(firstName, version);
            using var stream = new MemoryStream(content, writable: false);
            if (await client.UploadIfAbsentAsync(name, stream, cancellationToken))
            {
                return name;
            }
        }

        throw new InvalidOperationException($"{firstName} already has {MaxVersions} versions");
    }

}
