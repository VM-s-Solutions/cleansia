using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Gdpr;

/// <summary>
/// What a <c>customer.consent.grant</c> / <c>customer.consent.withdraw</c> row records: the type and
/// the document version — on a withdrawal, the version the row was granted under (ADR-0062 D4).
/// </summary>
public record ConsentEvidence(ConsentType ConsentType, string? DocumentVersion) : ICustomerAuditPayload;
