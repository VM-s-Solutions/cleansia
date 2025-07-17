using Microsoft.EntityFrameworkCore.Storage;

namespace Cleansia.Core.Domain.SeedWork;

public interface IUnitOfWork : IDisposable
{
    void Rollback();
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}