namespace Cleansia.Core.Queue.Abstractions.Messages;

public record GenerateReceiptMessage(string OrderId, string LanguageCode);
