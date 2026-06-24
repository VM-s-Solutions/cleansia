using Cleansia.Core.AppServices.Features.Auditing.DTOs;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// AC2 / ADR-0012 D4.1 (projection discipline) — the snapshot blobs are returned ONLY by the
/// single-row read. The paged list DTO never carries BeforeJson/AfterJson (so a bulk list cannot
/// stream snapshot payloads); the single-row detail DTO does. A future field added to the paged DTO
/// that re-introduces a snapshot blob trips this guard.
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
}
