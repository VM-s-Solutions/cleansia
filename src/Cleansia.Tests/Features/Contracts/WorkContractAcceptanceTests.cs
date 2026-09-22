using Cleansia.Core.Domain.Contracts;
using Cleansia.TestUtilities;

namespace Cleansia.Tests.Features.Contracts;

/// <summary>
/// ADR-0068 D2 — the row records exactly what the acceptor hands it, stamps the legal instant from the
/// server clock, and the one mutator blanks the three request-metadata columns and nothing else.
/// </summary>
public sealed class WorkContractAcceptanceTests
{
    private static WorkContractAcceptance Create() =>
        WorkContractAcceptance.Create(
            orderId: "01ORDER000000000000000001",
            orderEmployeeId: "01SEAT0000000000000000001",
            employeeId: "01EMP00000000000000000001",
            text: WorkContractTestData.Document().TextFor("cs")!,
            documentVersion: WorkContractTestData.Version,
            clientAudience: "cleansia.mobile",
            ipAddress: "203.0.113.9",
            deviceLabel: "Pixel 8",
            deviceId: "device-claim-1",
            factsJson: "{\"orderNumber\":\"ORD-1\"}");

    [Fact]
    public void Create_Records_The_Seat_The_Text_Row_And_The_Request_Context_As_Given()
    {
        var acceptance = Create();

        Assert.Equal("01ORDER000000000000000001", acceptance.OrderId);
        Assert.Equal("01SEAT0000000000000000001", acceptance.OrderEmployeeId);
        Assert.Equal("01EMP00000000000000000001", acceptance.EmployeeId);
        Assert.Equal(WorkContractTestData.TextIdCs, acceptance.LegalDocumentTextId);
        Assert.Equal(WorkContractTestData.Version, acceptance.DocumentVersion);
        Assert.Equal("cleansia.mobile", acceptance.ClientAudience);
        Assert.Equal("203.0.113.9", acceptance.IpAddress);
        Assert.Equal("Pixel 8", acceptance.DeviceLabel);
        Assert.Equal("device-claim-1", acceptance.DeviceId);
        Assert.Equal("{\"orderNumber\":\"ORD-1\"}", acceptance.FactsJson);
        Assert.Null(acceptance.TenantId);
        Assert.Equal(26, acceptance.Id.Length);
    }

    [Fact]
    public void Create_Stamps_The_Legal_Instant_From_The_Server_Clock()
    {
        var before = DateTimeOffset.UtcNow;

        var acceptance = Create();

        Assert.InRange(acceptance.AcceptedOn, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, acceptance.AcceptedOn.Offset);
    }

    [Fact]
    public void Pseudonymise_Blanks_Exactly_The_Trio_And_Keeps_The_Evidence()
    {
        var acceptance = Create();

        acceptance.Pseudonymise();

        Assert.Null(acceptance.IpAddress);
        Assert.Null(acceptance.DeviceLabel);
        Assert.Null(acceptance.DeviceId);
        Assert.Equal("01SEAT0000000000000000001", acceptance.OrderEmployeeId);
        Assert.Equal("01EMP00000000000000000001", acceptance.EmployeeId);
        Assert.Equal(WorkContractTestData.TextIdCs, acceptance.LegalDocumentTextId);
        Assert.Equal("{\"orderNumber\":\"ORD-1\"}", acceptance.FactsJson);
        Assert.Equal("cleansia.mobile", acceptance.ClientAudience);
    }
}
