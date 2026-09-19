namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// The typed evidence record a customer act passes to <see cref="IAuditContext.RecordEvidence"/>
/// (ADR-0062 D3). Empty on purpose: it exists so a test can find every payload and prove none carries
/// a name, contact detail or free text — ids, money, enums and versions only.
/// </summary>
public interface ICustomerAuditPayload;
