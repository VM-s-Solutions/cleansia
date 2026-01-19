using Cleansia.Core.Domain.Company;

namespace Cleansia.Core.Domain.Repositories;

public interface ICompanyInfoRepository : IRepository<CompanyInfo, string>
{
    Task<CompanyInfo?> GetActiveCompanyInfoAsync(CancellationToken cancellationToken);
    Task<CompanyInfo?> GetActiveByCountryAsync(string countryId, CancellationToken cancellationToken);
    Task<bool> ExistsActiveForCountryAsync(string countryId, CancellationToken cancellationToken);
    Task<bool> ExistsActiveForCountryExcludingAsync(string countryId, string excludeId, CancellationToken cancellationToken);
    Task<int> CountActiveAsync(CancellationToken cancellationToken);
}
