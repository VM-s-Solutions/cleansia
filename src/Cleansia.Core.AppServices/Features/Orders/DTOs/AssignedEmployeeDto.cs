namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

public record AssignedEmployeeDto(
    string Id,
    string EmployeeId,
    string FullName,
    string? PhoneNumber,
    string? Email
);
