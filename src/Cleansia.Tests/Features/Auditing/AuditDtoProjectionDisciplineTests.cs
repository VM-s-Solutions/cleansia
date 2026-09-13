using Cleansia.Core.AppServices.Features.Auditing.DTOs;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// AC2 / ADR-0012 D4.1 (projection discipline) — the snapshot blobs are returned ONLY by the
/// single-row read. The paged list DTO never carries BeforeJson/AfterJson (so a bulk list cannot
/// stream snapshot payloads); the single-row detail DTO does. A future field added to the paged DTO
/// that re-introduces a snapshot blob trips this guard. ADR-0062 D6 holds the customer table to the
/// same line: the payload and the three request-metadata columns are single-row only, and neither
/// DTO carries the tenant.
/// </summary>
public class AuditDtoProjectionDisciplineTests
{
    [Fact]
    public void Paged_Dto_Omits_The_Snapshot_Blobs()
    {
        var names = typeof(AdminActionAuditDto).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain(nameof(AdminActionAuditDetailDto.BeforeJson), names);
        Assert.DoesNotContain(nameof(AdminActionAuditDetailDto.AfterJson), names);
    }

    [Fact]
    public void SingleRow_Detail_Dto_Includes_The_Snapshot_Blobs()
    {
        var names = typeof(AdminActionAuditDetailDto).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Contains(nameof(AdminActionAuditDetailDto.BeforeJson), names);
        Assert.Contains(nameof(AdminActionAuditDetailDto.AfterJson), names);
    }

    [Fact]
    public void Customer_Paged_Dto_Omits_The_Payload_And_The_Request_Metadata()
    {
        var names = typeof(CustomerActionAuditDto).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain(nameof(CustomerActionAuditDetailDto.PayloadJson), names);
        Assert.DoesNotContain(nameof(CustomerActionAuditDetailDto.IpAddress), names);
        Assert.DoesNotContain(nameof(CustomerActionAuditDetailDto.DeviceLabel), names);
        Assert.DoesNotContain(nameof(CustomerActionAuditDetailDto.DeviceId), names);
        Assert.DoesNotContain("TenantId", names);
    }

    [Fact]
    public void Customer_SingleRow_Detail_Dto_Includes_Them_And_Never_The_Tenant()
    {
        var names = typeof(CustomerActionAuditDetailDto).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Contains(nameof(CustomerActionAuditDetailDto.PayloadJson), names);
        Assert.Contains(nameof(CustomerActionAuditDetailDto.IpAddress), names);
        Assert.Contains(nameof(CustomerActionAuditDetailDto.DeviceLabel), names);
        Assert.Contains(nameof(CustomerActionAuditDetailDto.DeviceId), names);
        Assert.DoesNotContain("TenantId", names);
    }
}
