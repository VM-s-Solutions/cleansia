namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

// `EmployeeId` is load-bearing on the PARTNER side: the partner-app
// computes "am I assigned to this order?" by matching the caller's
// session employee id against this list. Dropping it breaks the
// take/start/complete-order capability checks.
//
// Customer-side it's a low-value disclosure — the cleaner's internal
// backend id with no exploitable surface (not a session token, not a
// Stripe customer, etc). Acceptable shared shape; split into
// PartnerAssignedEmployeeDto + CustomerAssignedEmployeeDto if customer-app
// telemetry ever shows the field being read on the client.
public record AssignedEmployeeDto(
    string Id,
    string EmployeeId,
    string FullName,
    string? PhoneNumber
);
