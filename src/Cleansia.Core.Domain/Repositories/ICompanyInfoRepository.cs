using Cleansia.Core.Domain.Company;

namespace Cleansia.Core.Domain.Repositories;

public interface ICompanyInfoRepository : IRepository<CompanyInfo, string>
{
    Task<CompanyInfo?> GetActiveCompanyInfoAsync(CancellationToken cancellationToken);
}
