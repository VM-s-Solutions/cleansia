namespace Cleansia.Core.Queue.Abstractions.Messages;

public record GenerateInvoiceMessage(string EmployeeId, string PayPeriodId, string LanguageCode);
