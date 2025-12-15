using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeeRepository : IRepository<Employee, string>
{
    Task<Employee?> GetByUserEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithUserEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<List<Employee>> GetAllActiveWithUserAsync(CancellationToken cancellationToken = default);
}