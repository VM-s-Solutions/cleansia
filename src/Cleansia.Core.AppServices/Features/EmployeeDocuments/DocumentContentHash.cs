using System.Security.Cryptography;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

internal static class DocumentContentHash
{
    public static string Of(BlobFileDto file) =>
        Convert.ToHexStringLower(SHA256.HashData(Convert.FromBase64String(file.Base64Content!.ExtractBase64Data())));

    public static async Task<bool> IsKeptByAsync(
        IEmployeeDocumentRepository documentRepository,
        string employeeId,
        IReadOnlyCollection<string> contentHashes,
        CancellationToken cancellationToken)
    {
        var activeDocuments = await documentRepository.GetByEmployeeIdAsync(
            employeeId, cancellationToken: cancellationToken);

        return activeDocuments.Any(document =>
            document.Status != DocumentStatus.Rejected && contentHashes.Contains(document.ContentSha256));
    }
}
