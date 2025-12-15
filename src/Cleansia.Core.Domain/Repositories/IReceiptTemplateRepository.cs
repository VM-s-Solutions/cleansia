using Cleansia.Core.Domain.ReceiptTemplates;

namespace Cleansia.Core.Domain.Repositories;

public interface IReceiptTemplateRepository : IRepository<ReceiptTemplate, string>
{
    Task<ReceiptTemplate?> GetActiveByCountryAndLanguageAsync(
        string countryId,
        string languageCode,
        CancellationToken cancellationToken);

    Task<int> GetNextVersionAsync(
        string countryId,
        string languageId,
        CancellationToken cancellationToken);

    Task<bool> ExistsActiveTemplateAsync(
        string countryId,
        string languageId,
        CancellationToken cancellationToken);
}
