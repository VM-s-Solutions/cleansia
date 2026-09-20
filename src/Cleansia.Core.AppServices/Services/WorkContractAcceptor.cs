using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Services;

public sealed class WorkContractAcceptor(
    ILegalDocumentRepository legalDocumentRepository,
    IWorkContractFactsBuilder factsBuilder,
    IWorkContractAcceptanceRepository acceptanceRepository,
    IEmployeeActionAuditRepository employeeActionAuditRepository,
    IRequestMetadataProvider requestMetadataProvider,
    IUserSessionProvider userSessionProvider,
    IHostAudienceProvider hostAudienceProvider) : IWorkContractAcceptor
{
    public async Task<WorkContractAcceptance> StageAsync(
        Order order, OrderEmployee seat, string textId, CancellationToken cancellationToken)
    {
        var document = await legalDocumentRepository.GetByTextIdWithTextsAsync(textId, cancellationToken);
        var text = document?.Texts.FirstOrDefault(t => t.Id == textId);

        // The validators are the gate; a text of another document reaching here is a programming error,
        // not a business refusal, and must never become a row.
        if (document is null || text is null || order.WorkContractDocumentId is null || document.Id != order.WorkContractDocumentId)
        {
            throw new InvalidOperationException(
                $"Text '{textId}' is not a text of order {order.Id}'s work-contract document '{order.WorkContractDocumentId}'.");
        }

        var facts = await factsBuilder.BuildAsync(order.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Order {order.Id} has no facts to freeze on its acceptance.");

        // The device is the session's signed claim or nothing — never the X-Device-Id header, which is
        // the client's word alone on a signed-in act.
        var deviceId = userSessionProvider.GetTypedUserClaim(AuthExtensions.DeviceIdClaimType)?.Value;

        var acceptance = WorkContractAcceptance.Create(
            orderId: order.Id,
            orderEmployeeId: seat.Id,
            employeeId: seat.EmployeeId,
            text: text,
            documentVersion: document.Version,
            clientAudience: hostAudienceProvider.Audience,
            ipAddress: requestMetadataProvider.IpAddress,
            deviceLabel: requestMetadataProvider.DeviceLabel,
            deviceId: deviceId,
            factsJson: facts.ToJson());

        // The row is a fact of the ORDER's books. Both callers load the order through the tenant filter,
        // so this is the ambient company CommitAsync would stamp anyway; pinning it to the order keeps
        // that true for a caller that ever loads the order outside the filter.
        acceptance.TenantId = order.TenantId;
        acceptanceRepository.Add(acceptance);

        var audit = EmployeeActionAudit.Create(seat.EmployeeId, order.Id, EmployeeAuditAction.ContractAccepted);
        audit.TenantId = order.TenantId;
        employeeActionAuditRepository.Add(audit);

        return acceptance;
    }
}
