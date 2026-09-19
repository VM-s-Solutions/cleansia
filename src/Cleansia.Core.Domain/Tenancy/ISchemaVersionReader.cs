namespace Cleansia.Core.Domain.Tenancy;

/// <summary>
/// The id of the migration the database was built from, for the archive manifest: a bundle read in
/// ten years must say which schema its rows were shaped by.
/// </summary>
public interface ISchemaVersionReader
{
    Task<string?> ReadAsync(CancellationToken cancellationToken);
}
