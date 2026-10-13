using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Queue.Abstractions.Messages;

public record SendOrderStatusEmailMessage(string OrderId, OrderStatus Status, string LanguageCode, string? TenantId)
{
    public const string Discriminator = "order-status";
    public string MessageType => Discriminator;
}
