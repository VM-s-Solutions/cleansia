using Microsoft.EntityFrameworkCore.Storage;

namespace Cleansia.Core.Domain.SeedWork;

public interface IUnitOfWork : IDisposable
{
    void Rollback();
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}