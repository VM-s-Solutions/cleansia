namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// Fan-out per cleaner from a completed order or a fee collected on a cancelled one: the queue consumer
/// invokes <c>CalculateOrderPay.Command</c> for this (OrderId, EmployeeId)
/// so the order's <c>OrderEmployeePay</c> row is created out-of-band — keeps
/// the partner mobile "Complete" tap fast and lets pay-calc retry independently
/// from the user-facing order completion.
/// </summary>
public record CalculateOrderPayMessage(string OrderId, string EmployeeId);
