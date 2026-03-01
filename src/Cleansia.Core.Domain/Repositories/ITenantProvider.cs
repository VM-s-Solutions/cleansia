namespace Cleansia.Core.Domain.Repositories;

public interface ITenantProvider
{
    string? GetCurrentTenantId();
}
