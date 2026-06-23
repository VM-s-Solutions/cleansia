using Cleansia.Config.Repositories;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Infra.Database.Auditing;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 — the audit seams are registered SCOPED with the right backings: IAuditContext is the
/// per-request snapshot buffer, IAuditWriter rides the pipeline's scoped DbContext, and IAuditFailureSink
/// is the out-of-band writer. Mirrors OutboxWiringTests.
/// </summary>
public sealed class AuditWiringTests
{
    [Theory]
    [InlineData(typeof(IAuditContext), typeof(AuditContext))]
    [InlineData(typeof(IAuditWriter), typeof(DbContextAuditWriter))]
    [InlineData(typeof(IAuditFailureSink), typeof(OutOfBandAuditFailureSink))]
    public void Repositories_Register_The_Audit_Seams_Scoped(Type service, Type implementation)
    {
        var services = new ServiceCollection().AddRepositories();

        var descriptor = Assert.Single(services, d => d.ServiceType == service);
        Assert.Equal(implementation, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void Repositories_Register_The_AuditEntryFactory_Scoped()
    {
        var services = new ServiceCollection().AddRepositories();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(AuditEntryFactory));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
