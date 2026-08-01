namespace Cleansia.Core.AppServices.Shared.DTOs.Files;

// BlobUrl is server-populated on READS only — a short-lived read SAS for FileName, null on request
// payloads and wherever the producing mapper had no blob client. It EXPIRES, and its query string
// changes on every fetch, so clients key their image cache on FileName and never on this value.
// FileName is content-addressed: every upload mints a new name, so a changed avatar always changes
// the key and no eviction step is needed on save.
public record BlobFileDto(
    string FileName,
    string? Base64Content,
    string? ContentType,
    string? BlobUrl = null);